using Fluxy.Application.Streams.Utils;
using Fluxy.Application.Content.Utils;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Repositories;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Utils;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Interfaces;
using Fluxy.Core.Streams.Outcomes;
using Fluxy.Core.Streams.Repositories;
using Fluxy.Core.Streams.Rows;
using Fluxy.Core.Streams.Utils;

namespace Fluxy.Application.Streams.Services
{
    /// <summary>
    /// Turns the entries of an uploaded M3U playlist into audio stream rows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The import is a partial success by design. A file assembled by hand has entries that are
    /// not absolute URLs, titles that are too long, and names already used in the category; none
    /// of those is a reason to discard the other four thousand entries. Every unusable line is
    /// reported with the line it came from, and the usable ones are written in one transaction.
    /// The only all-or-nothing failures are the ones that make the request itself unusable -
    /// the settings shared by the batch, the category, a file that is not a stream list, and a
    /// name another request took between the check and the write.
    /// </para>
    /// <para>
    /// The playlist reading is shared with the video import, which is why the entry limit is
    /// passed explicitly instead of relying on the parser's default: the default belongs to the
    /// other import, and a limit that changes there must not silently change this one.
    /// </para>
    /// </remarks>
    public sealed class AudioStreamImportService : IAudioStreamImportService
    {
        private readonly IAudioStreamRepository _streams;
        private readonly IBouquetRepository _bouquets;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioStreamImportService"/> class.
        /// </summary>
        /// <param name="streams">Repository holding the streams and their sources.</param>
        /// <param name="bouquets">
        /// Repository staging the membership rows that put the imported streams in the
        /// bouquets the request named.
        /// </param>
        /// <param name="unitOfWork">Commits the batch one operation stages.</param>
        public AudioStreamImportService(
            IAudioStreamRepository streams,
            IBouquetRepository bouquets,
            IUnitOfWork unitOfWork)
        {
            _streams = streams;
            _bouquets = bouquets;
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public async Task<AudioStreamImportOutcome> ImportAsync(
            NewAudioStreamImport input,
            CancellationToken cancellationToken = default)
        {
            var errors = AudioStreamRules.ValidateCommon(
                input.Type,
                input.Status,
                input.CategoryId,
                input.RestreamUserAgent,
                input.RestreamProxy);

            if (Enum.IsDefined(input.Type) && !IsImportable(input.Type))
            {
                errors[nameof(NewAudioStream.Type)] = AudioStreamRules.ImportTypeRule;
            }

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            if (await _streams.CategoryIsUsableAsync(input.CategoryId, cancellationToken) is false)
            {
                return Invalid(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [nameof(NewAudioStream.CategoryId)] = AudioStreamRules.CategoryRule
                });
            }

            var playlist = M3uPlaylistParser.Parse(
                input.Playlist,
                AudioStreamImportLimits.MaxEntries);

            switch (playlist.Status)
            {
                case M3uPlaylistStatus.Empty:
                    return Playlist(M3uPlaylistStatus.Empty);

                case M3uPlaylistStatus.Hls:
                    return Playlist(M3uPlaylistStatus.Hls);

                case M3uPlaylistStatus.TooManyEntries:
                    return Playlist(M3uPlaylistStatus.TooManyEntries);

                case M3uPlaylistStatus.Unreadable:
                    return Playlist(M3uPlaylistStatus.Unreadable);
            }

            var problems = new List<AudioStreamImportProblem>();
            var failed = 0;

            foreach (var problem in playlist.Problems)
            {
                problems.Add(new AudioStreamImportProblem
                {
                    LineNumber = problem.LineNumber,
                    Name = problem.Name,
                    Action = AudioStreamAction.Invalid,
                    Message = problem.Message,
                    Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        [nameof(M3uPlaylistEntry.Source)] = [problem.Message]
                    }
                });

                failed++;
            }

            var candidates = new List<M3uPlaylistEntry>();

            foreach (var entry in playlist.Entries)
            {
                var name = entry.Name.Trim();
                var source = entry.Source.Trim();

                if (!AudioStreamRules.IsUsableName(name))
                {
                    problems.Add(InvalidEntry(
                        entry.LineNumber,
                        name,
                        nameof(M3uPlaylistEntry.Name),
                        AudioStreamRules.NameRule));
                    failed++;
                    continue;
                }

                if (source.Length is < 1 or > AudioStreamLimits.SourceUrlMaxLength)
                {
                    problems.Add(InvalidEntry(
                        entry.LineNumber,
                        name,
                        nameof(M3uPlaylistEntry.Source),
                        AudioStreamRules.SourcesRule));
                    failed++;
                    continue;
                }

                candidates.Add(entry with { Name = name, Source = source });
            }

            var existing = candidates.Count == 0
                ? new List<AudioStreamImportRow>()
                : await _streams.GetImportRowsAsync(
                    input.CategoryId,
                    candidates.Select(entry => entry.Name).ToArray(),
                    cancellationToken);

            var existingByName = existing
                .ToDictionary(row => row.Name, StringComparer.Ordinal);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var writes = new List<AudioStreamWrite>();
            var updates = new List<AudioStreamBatchUpdate>();
            var skipped = 0;
            var updated = 0;

            foreach (var entry in candidates)
            {
                // A name used twice inside the same file can only win once.
                if (!seen.Add(entry.Name))
                {
                    problems.Add(Skipped(entry, "Another stream of this category already has that name."));
                    skipped++;
                    continue;
                }

                if (existingByName.TryGetValue(entry.Name, out var row))
                {
                    // Without an update flag the row is left alone, exactly as before.
                    if (!input.AppendSourceToExisting && !input.OverwriteExistingSettings)
                    {
                        problems.Add(Skipped(
                            entry,
                            "Another stream of this category already has that name."));
                        skipped++;
                        continue;
                    }

                    updates.Add(new AudioStreamBatchUpdate
                    {
                        Id = row.Id,
                        Type = input.Type,
                        Status = input.Status,
                        RestreamUserAgent = AudioStreamRules.NullIfBlank(
                            input.RestreamUserAgent),
                        RestreamProxy = AudioStreamRules.NullIfBlank(input.RestreamProxy),
                        RestreamCookies = AudioStreamRules.NullIfBlank(input.RestreamCookies),
                        RestreamHeaders = AudioStreamRules.NullIfBlank(input.RestreamHeaders),
                        SourceToAppend = entry.Source,
                        AppendSource = input.AppendSourceToExisting,
                        OverwriteSettings = input.OverwriteExistingSettings
                    });

                    updated++;
                    continue;
                }

                writes.Add(BuildWrite(input, entry));
            }

            var total = playlist.Entries.Count + playlist.Problems.Count;

            if (writes.Count > 0)
            {
                var created = await _streams.AddRangeAsync(writes, cancellationToken);

                // The bouquets the import named go on the rows it created, and only on
                // those: a skipped or updated entry is a row that already existed, and an
                // import is not an edit of somebody else's packaging.
                await BouquetMembership.StageManyAsync(
                    _bouquets,
                    ContentType.AudioStream,
                    created,
                    input.BouquetIds,
                    cancellationToken);
            }

            if (updates.Count > 0)
            {
                await _streams.UpdateRangeAsync(updates, cancellationToken);
            }

            if (writes.Count == 0 && updates.Count == 0)
            {
                return new AudioStreamImportOutcome
                {
                    Status = AudioStreamImportStatus.Completed,
                    Total = total,
                    Imported = 0,
                    Updated = 0,
                    Skipped = skipped,
                    Failed = failed,
                    Problems = Order(problems)
                };
            }

            if (await _unitOfWork.TrySaveAsync(cancellationToken) is { } failure)
            {
                // Only the name index is a fact this service can explain. A violation of the
                // source position index is a bug in the list writer and is rethrown, so it
                // cannot wear the answer about a taken name.
                if (!failure.IsConstraint(AudioStreamRules.NameIndexName))
                {
                    throw failure;
                }

                return new AudioStreamImportOutcome
                {
                    Status = AudioStreamImportStatus.Conflict,
                    Total = total,
                    Imported = 0,
                    Updated = 0,
                    Skipped = skipped,
                    Failed = failed,
                    Problems = Order(problems)
                };
            }

            return new AudioStreamImportOutcome
            {
                Status = AudioStreamImportStatus.Completed,
                Total = total,
                Imported = writes.Count,
                Updated = updated,
                Skipped = skipped,
                Failed = failed,
                Problems = Order(problems)
            };
        }

        /// <summary>Whether an import may create a stream of this kind.</summary>
        private static bool IsImportable(AudioStreamType type)
            => type is AudioStreamType.OnDemand or AudioStreamType.Redirect;

        /// <summary>Builds the row one surviving group of settings and one entry produce.</summary>
        private static AudioStreamWrite BuildWrite(
            NewAudioStreamImport input,
            M3uPlaylistEntry entry) => new()
            {
                Name = entry.Name,
                Type = input.Type,
                Status = input.Status,
                CategoryId = input.CategoryId,
                LogoUrl = LogoFor(entry),
                RestreamUserAgent = AudioStreamRules.NullIfBlank(input.RestreamUserAgent),
                RestreamProxy = AudioStreamRules.NullIfBlank(input.RestreamProxy),
                RestreamCookies = AudioStreamRules.NullIfBlank(input.RestreamCookies),
                RestreamHeaders = AudioStreamRules.NullIfBlank(input.RestreamHeaders),
                Sources = [entry.Source]
            };

        /// <summary>
        /// The logo an entry asks for, or null when there is none or it is too long to store.
        /// A logo that overshoots the column is dropped rather than refused - metadata must not
        /// spoil an otherwise usable stream, and the create form never validates it either.
        /// </summary>
        private static string? LogoFor(M3uPlaylistEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.LogoUrl))
            {
                return null;
            }

            var logo = entry.LogoUrl.Trim();
            return logo.Length <= AudioStreamLimits.LogoUrlMaxLength ? logo : null;
        }

        /// <summary>Builds the problem one entry with a rejected field produces.</summary>
        private static AudioStreamImportProblem InvalidEntry(
            int lineNumber,
            string? name,
            string field,
            string[] sentences) => new()
            {
                LineNumber = lineNumber,
                Name = name,
                Action = AudioStreamAction.Invalid,
                Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [field] = sentences
                }
            };

        /// <summary>Builds the problem one entry whose name is already taken produces.</summary>
        private static AudioStreamImportProblem Skipped(M3uPlaylistEntry entry, string message) => new()
        {
            LineNumber = entry.LineNumber,
            Name = entry.Name,
            Action = AudioStreamAction.AlreadyExists,
            Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(M3uPlaylistEntry.Name)] = [message]
            }
        };

        /// <summary>Puts the report in the order the operator will find the lines in the file.</summary>
        private static IReadOnlyList<AudioStreamImportProblem> Order(
            List<AudioStreamImportProblem> problems)
            => problems
                .OrderBy(problem => problem.LineNumber)
                .ToList();

        private static AudioStreamImportOutcome Invalid(
            IReadOnlyDictionary<string, string[]> errors) => new()
            {
                Status = AudioStreamImportStatus.Invalid,
                Errors = errors
            };

        private static AudioStreamImportOutcome Playlist(M3uPlaylistStatus status) => new()
        {
            Status = status switch
            {
                M3uPlaylistStatus.Empty => AudioStreamImportStatus.PlaylistEmpty,
                M3uPlaylistStatus.Hls => AudioStreamImportStatus.PlaylistHls,
                M3uPlaylistStatus.TooManyEntries => AudioStreamImportStatus.PlaylistTooManyEntries,
                _ => AudioStreamImportStatus.PlaylistUnreadable
            }
        };
    }
}
