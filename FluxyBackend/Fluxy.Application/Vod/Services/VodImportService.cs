using Fluxy.Application.Vod.Utils;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Utils;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Outcomes;
using Fluxy.Core.Streams.Utils;
using Fluxy.Core.Vod.Commands;
using Fluxy.Core.Vod.Enums;
using Fluxy.Core.Vod.Interfaces;
using Fluxy.Core.Vod.Outcomes;
using Fluxy.Core.Vod.Repositories;
using Fluxy.Core.Vod.Rows;
using Fluxy.Core.Vod.Utils;

namespace Fluxy.Application.Vod.Services
{
    /// <summary>
    /// Turns the entries of an uploaded M3U playlist into on-demand items.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The import is a partial success by design. A file assembled by hand has entries that
    /// are not absolute URLs, titles that are too long and names already used in the category;
    /// none of those is a reason to discard the other four thousand entries. Every unusable
    /// line is reported with the line it came from, and the usable ones are written in one
    /// transaction.
    /// </para>
    /// <para>
    /// The playlist reader is the one the streams already use. It is a Core utility of the
    /// layer rather than a rule of the stream domain, and the entry limit is passed explicitly
    /// so this import's bound is its own rather than the reader's default.
    /// </para>
    /// </remarks>
    public sealed class VodImportService : IVodImportService
    {
        private readonly IVodRepository _items;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="VodImportService"/> class.
        /// </summary>
        /// <param name="items">Repository holding the items.</param>
        /// <param name="unitOfWork">Commits the batch one operation stages.</param>
        public VodImportService(IVodRepository items, IUnitOfWork unitOfWork)
        {
            _items = items;
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public async Task<VodImportOutcome> ImportAsync(
            NewVodImport input,
            CancellationToken cancellationToken = default)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (!Enum.IsDefined(input.Type))
            {
                errors[nameof(NewVodImport.Type)] = VodRules.TypeRule;
            }

            if (!Enum.IsDefined(input.Status))
            {
                errors[nameof(NewVodImport.Status)] = VodRules.StatusRule;
            }

            if (input.CategoryId == Guid.Empty)
            {
                errors[nameof(NewVodImport.CategoryId)] = VodRules.CategoryRule;
            }

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            if (await _items.CategoryIsUsableAsync(input.CategoryId, cancellationToken) is false)
            {
                return Invalid(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [nameof(NewVodImport.CategoryId)] = VodRules.CategoryRule
                });
            }

            var playlist = M3uPlaylistParser.Parse(input.Playlist, VodImportLimits.MaxEntries);

            switch (playlist.Status)
            {
                case M3uPlaylistStatus.Empty:
                    return Playlist(VodImportStatus.PlaylistEmpty);

                case M3uPlaylistStatus.Hls:
                    return Playlist(VodImportStatus.PlaylistHls);

                case M3uPlaylistStatus.TooManyEntries:
                    return Playlist(VodImportStatus.PlaylistTooManyEntries);

                case M3uPlaylistStatus.Unreadable:
                    return Playlist(VodImportStatus.PlaylistUnreadable);
            }

            var problems = new List<VodImportProblem>();
            var failed = 0;

            foreach (var problem in playlist.Problems)
            {
                problems.Add(new VodImportProblem
                {
                    LineNumber = problem.LineNumber,
                    Name = problem.Name,
                    Action = VodAction.Invalid,
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

                if (!VodRules.IsUsableName(name))
                {
                    problems.Add(InvalidEntry(
                        entry.LineNumber,
                        name,
                        nameof(M3uPlaylistEntry.Name),
                        VodRules.NameRule));
                    failed++;
                    continue;
                }

                if (!VodRules.IsUsableSource(source))
                {
                    problems.Add(InvalidEntry(
                        entry.LineNumber,
                        name,
                        nameof(M3uPlaylistEntry.Source),
                        VodRules.SourceRule));
                    failed++;
                    continue;
                }

                candidates.Add(entry with { Name = name, Source = source });
            }

            var existing = candidates.Count == 0
                ? new List<VodImportRow>()
                : await _items.GetImportRowsAsync(
                    input.CategoryId,
                    candidates.Select(entry => entry.Name).ToArray(),
                    cancellationToken);

            var existingByName = existing.ToDictionary(row => row.Name, StringComparer.Ordinal);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var writes = new List<VodItemWrite>();
            var skipped = 0;

            foreach (var entry in candidates)
            {
                // A name used twice inside the same file can only win once.
                if (!seen.Add(entry.Name))
                {
                    problems.Add(new VodImportProblem
                    {
                        LineNumber = entry.LineNumber,
                        Name = entry.Name,
                        Action = VodAction.AlreadyExists,
                        Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                        {
                            [nameof(M3uPlaylistEntry.Name)] =
                                ["Another video of this category already has that name."]
                        }
                    });
                    skipped++;
                    continue;
                }

                if (existingByName.ContainsKey(entry.Name))
                {
                    problems.Add(new VodImportProblem
                    {
                        LineNumber = entry.LineNumber,
                        Name = entry.Name,
                        Action = VodAction.AlreadyExists,
                        Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                        {
                            [nameof(M3uPlaylistEntry.Name)] =
                                ["Another video of this category already has that name."]
                        }
                    });
                    skipped++;
                    continue;
                }

                writes.Add(BuildWrite(input, entry));
            }

            var total = playlist.Entries.Count + playlist.Problems.Count;

            if (writes.Count > 0)
            {
                await _items.AddRangeAsync(writes, cancellationToken);

                if (await _unitOfWork.TrySaveAsync(cancellationToken) is { } failure)
                {
                    // Only the name index is a fact this service can explain. Any other unique
                    // violation is a bug in the write and is rethrown, so it cannot wear the
                    // answer about a taken name.
                    if (!failure.IsConstraint(VodRules.NameIndexName))
                    {
                        throw failure;
                    }

                    return new VodImportOutcome
                    {
                        Status = VodImportStatus.Conflict,
                        Total = total,
                        Imported = 0,
                        Skipped = skipped,
                        Failed = failed,
                        Problems = Order(problems)
                    };
                }
            }

            return new VodImportOutcome
            {
                Status = VodImportStatus.Completed,
                Total = total,
                Imported = writes.Count,
                Skipped = skipped,
                Failed = failed,
                Problems = Order(problems)
            };
        }

        /// <summary>Builds the row one entry and the shared settings produce.</summary>
        private static VodItemWrite BuildWrite(NewVodImport input, M3uPlaylistEntry entry) => new()
        {
            Name = entry.Name,
            Status = input.Status,
            Type = input.Type,
            CategoryId = input.CategoryId,
            Source = entry.Source,
            PosterUrl = PosterFor(entry),
            Cast = [],
            Genres = []
        };

        /// <summary>
        /// The poster an entry asks for, or null when there is none or it is too long to
        /// store. A poster that overshoots the column is dropped rather than refused -
        /// metadata must not spoil an otherwise usable item.
        /// </summary>
        private static string? PosterFor(M3uPlaylistEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.LogoUrl))
            {
                return null;
            }

            var poster = entry.LogoUrl.Trim();

            return poster.Length <= VodLimits.ImageUrlMaxLength ? poster : null;
        }

        /// <summary>Builds the problem one entry with a rejected field produces.</summary>
        private static VodImportProblem InvalidEntry(
            int lineNumber,
            string? name,
            string field,
            string[] sentences) => new()
            {
                LineNumber = lineNumber,
                Name = name,
                Action = VodAction.Invalid,
                Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [field] = sentences
                }
            };

        /// <summary>Puts the report in the order the operator will find the lines in the file.</summary>
        private static IReadOnlyList<VodImportProblem> Order(List<VodImportProblem> problems)
            => problems.OrderBy(problem => problem.LineNumber).ToList();

        private static VodImportOutcome Invalid(IReadOnlyDictionary<string, string[]> errors) => new()
        {
            Status = VodImportStatus.Invalid,
            Errors = errors
        };

        private static VodImportOutcome Playlist(VodImportStatus status) => new() { Status = status };
    }
}
