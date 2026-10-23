using Fluxy.Application.Streams.Utils;
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
    /// Turns the entries of an uploaded M3U playlist into stream rows.
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
    /// The rules a row must satisfy are not restated here. The common half is
    /// <see cref="VideoStreamRules"/>, the playlist reading is <see cref="M3uPlaylistParser"/>,
    /// and the position is the highest of the category plus one, read from the repository. What
    /// belongs to this service is the batch: which entries survive, which are skipped rather
    /// than failed, and the one save that writes them all.
    /// </para>
    /// </remarks>
    public sealed class VideoStreamImportService : IVideoStreamImportService
    {
        private readonly IVideoStreamRepository _streams;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoStreamImportService"/> class.
        /// </summary>
        /// <param name="streams">Repository holding the streams and their two lists.</param>
        /// <param name="unitOfWork">Commits the batch one operation stages.</param>
        public VideoStreamImportService(
            IVideoStreamRepository streams,
            IUnitOfWork unitOfWork)
        {
            _streams = streams;
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public async Task<VideoStreamImportOutcome> ImportAsync(
            NewVideoStreamImport input,
            CancellationToken cancellationToken = default)
        {
            var errors = VideoStreamRules.ValidateCommon(
                input.Type,
                input.Status,
                input.CategoryId,
                input.DelayMinutes,
                input.ArchiveDays,
                input.RestreamUserAgent,
                input.RestreamProxy);

            // The import form offers three kinds because the other three carry a series, a
            // video-playlist item or a file list that a playlist of URLs cannot supply. The
            // check is separate from the enum check so a value outside the enum still gets the
            // sentence that names every member.
            if (Enum.IsDefined(input.Type) && !VideoStreamRules.IsUrlKind(input.Type))
            {
                errors[nameof(NewVideoStream.Type)] = VideoStreamRules.ImportTypeRule;
            }

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            if (await _streams.CategoryIsUsableAsync(input.CategoryId, cancellationToken) is false)
            {
                return Invalid(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [nameof(NewVideoStream.CategoryId)] = VideoStreamRules.CategoryRule
                });
            }

            var playlist = M3uPlaylistParser.Parse(input.Playlist);

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

            var problems = new List<VideoStreamImportProblem>();
            var failed = 0;

            foreach (var problem in playlist.Problems)
            {
                problems.Add(new VideoStreamImportProblem
                {
                    LineNumber = problem.LineNumber,
                    Name = problem.Name,
                    Action = VideoStreamAction.Invalid,
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

                if (!VideoStreamRules.IsUsableName(name))
                {
                    problems.Add(InvalidEntry(
                        entry.LineNumber,
                        name,
                        nameof(M3uPlaylistEntry.Name),
                        VideoStreamRules.NameRule));
                    failed++;
                    continue;
                }

                if (source.Length is < 1 or > VideoStreamLimits.SourceUrlMaxLength)
                {
                    problems.Add(InvalidEntry(
                        entry.LineNumber,
                        name,
                        nameof(M3uPlaylistEntry.Source),
                        VideoStreamRules.SourcesRule));
                    failed++;
                    continue;
                }

                candidates.Add(entry with { Name = name, Source = source });
            }

            var existing = candidates.Count == 0
                ? new List<VideoStreamImportRow>()
                : await _streams.GetImportRowsAsync(
                    input.CategoryId,
                    candidates.Select(entry => entry.Name).ToArray(),
                    cancellationToken);

            var existingByName = existing
                .ToDictionary(row => row.Name, StringComparer.Ordinal);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var writes = new List<VideoStreamWrite>();
            var updates = new List<VideoStreamBatchUpdate>();
            var skipped = 0;
            var updated = 0;

            foreach (var entry in candidates)
            {
                // A name used twice inside the same file can only win once.
                if (!seen.Add(entry.Name))
                {
                    problems.Add(new VideoStreamImportProblem
                    {
                        LineNumber = entry.LineNumber,
                        Name = entry.Name,
                        Action = VideoStreamAction.AlreadyExists,
                        Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                        {
                            [nameof(M3uPlaylistEntry.Name)] =
                                ["Another stream of this category already has that name."]
                        }
                    });
                    skipped++;
                    continue;
                }

                if (existingByName.TryGetValue(entry.Name, out var row))
                {
                    // Without an update flag the row is left alone, exactly as before.
                    if (!input.AppendSourceToExisting && !input.OverwriteExistingSettings)
                    {
                        problems.Add(new VideoStreamImportProblem
                        {
                            LineNumber = entry.LineNumber,
                            Name = entry.Name,
                            Action = VideoStreamAction.AlreadyExists,
                            Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                            {
                                [nameof(M3uPlaylistEntry.Name)] =
                                    ["Another stream of this category already has that name."]
                            }
                        });
                        skipped++;
                        continue;
                    }

                    // Appending a source only makes sense for a kind that relays from sources.
                    // Anything else is a name collision the chosen options cannot act on.
                    if (input.AppendSourceToExisting && !input.OverwriteExistingSettings &&
                        !VideoStreamRules.IsUrlKind(row.Type))
                    {
                        problems.Add(new VideoStreamImportProblem
                        {
                            LineNumber = entry.LineNumber,
                            Name = entry.Name,
                            Action = VideoStreamAction.AlreadyExists,
                            Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                            {
                                [nameof(M3uPlaylistEntry.Name)] =
                                    ["Another stream of this category already has that name."]
                            }
                        });
                        skipped++;
                        continue;
                    }

                    updates.Add(new VideoStreamBatchUpdate
                    {
                        Id = row.Id,
                        Type = input.Type,
                        Status = input.Status,
                        DelayMinutes = input.DelayMinutes,
                        ArchiveDays = input.ArchiveDays,
                        RestreamUserAgent = VideoStreamRules.NullIfBlank(
                            input.RestreamUserAgent),
                        RestreamProxy = VideoStreamRules.NullIfBlank(input.RestreamProxy),
                        RestreamCookies = VideoStreamRules.NullIfBlank(input.RestreamCookies),
                        RestreamHeaders = VideoStreamRules.NullIfBlank(input.RestreamHeaders),
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
                var highest = await _streams.GetLastSortOrderAsync(
                    input.CategoryId,
                    exceptStream: null,
                    cancellationToken);

                await _streams.AddRangeAsync(writes, highest + 1, cancellationToken);
            }

            if (updates.Count > 0)
            {
                await _streams.UpdateRangeAsync(updates, cancellationToken);
            }

            if (writes.Count == 0 && updates.Count == 0)
            {
                return new VideoStreamImportOutcome
                {
                    Status = VideoStreamImportStatus.Completed,
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
                // Only the name index is a fact this service can explain. A violation of
                // either position index is a bug in the list writer and is rethrown, so it
                // cannot wear the answer about a taken name.
                if (!failure.IsConstraint(VideoStreamRules.NameIndexName))
                {
                    throw failure;
                }

                return new VideoStreamImportOutcome
                {
                    Status = VideoStreamImportStatus.Conflict,
                    Total = total,
                    Imported = 0,
                    Updated = 0,
                    Skipped = skipped,
                    Failed = failed,
                    Problems = Order(problems)
                };
            }

            return new VideoStreamImportOutcome
            {
                Status = VideoStreamImportStatus.Completed,
                Total = total,
                Imported = writes.Count,
                Updated = updated,
                Skipped = skipped,
                Failed = failed,
                Problems = Order(problems)
            };
        }

        /// <summary>Builds the row one surviving group of settings and one entry produce.</summary>
        private static VideoStreamWrite BuildWrite(
            NewVideoStreamImport input,
            M3uPlaylistEntry entry) => new()
            {
                Name = entry.Name,
                Type = input.Type,
                Status = input.Status,
                CategoryId = input.CategoryId,
                LogoUrl = LogoFor(entry),
                DelayMinutes = input.DelayMinutes,
                ArchiveDays = input.ArchiveDays,
                RestreamUserAgent = VideoStreamRules.NullIfBlank(input.RestreamUserAgent),
                RestreamProxy = VideoStreamRules.NullIfBlank(input.RestreamProxy),
                RestreamCookies = VideoStreamRules.NullIfBlank(input.RestreamCookies),
                RestreamHeaders = VideoStreamRules.NullIfBlank(input.RestreamHeaders),
                Sources = [entry.Source],
                Files = []
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
            return logo.Length <= VideoStreamLimits.LogoUrlMaxLength ? logo : null;
        }

        /// <summary>Builds the problem one entry with a rejected field produces.</summary>
        private static VideoStreamImportProblem InvalidEntry(
            int lineNumber,
            string? name,
            string field,
            string[] sentences) => new()
            {
                LineNumber = lineNumber,
                Name = name,
                Action = VideoStreamAction.Invalid,
                Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [field] = sentences
                }
            };

        /// <summary>Puts the report in the order the operator will find the lines in the file.</summary>
        private static IReadOnlyList<VideoStreamImportProblem> Order(
            List<VideoStreamImportProblem> problems)
            => problems
                .OrderBy(problem => problem.LineNumber)
                .ToList();

        private static VideoStreamImportOutcome Invalid(
            IReadOnlyDictionary<string, string[]> errors) => new()
            {
                Status = VideoStreamImportStatus.Invalid,
                Errors = errors
            };

        private static VideoStreamImportOutcome Playlist(M3uPlaylistStatus status) => new()
        {
            Status = status switch
            {
                M3uPlaylistStatus.Empty => VideoStreamImportStatus.PlaylistEmpty,
                M3uPlaylistStatus.Hls => VideoStreamImportStatus.PlaylistHls,
                M3uPlaylistStatus.TooManyEntries => VideoStreamImportStatus.PlaylistTooManyEntries,
                _ => VideoStreamImportStatus.PlaylistUnreadable
            }
        };
    }
}
