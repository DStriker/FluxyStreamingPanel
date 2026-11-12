using Fluxy.Core.Content.Commands;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Interfaces;
using Fluxy.Core.Content.Outcomes;
using Fluxy.Core.Content.Repositories;
using Fluxy.Core.Content.Rows;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Utils;
using Fluxy.Core.Series.Utils;
using Fluxy.Core.Streams.Utils;
using Fluxy.Core.Vod.Utils;

namespace Fluxy.Application.Content.Services
{
    /// <summary>
    /// Finds a string in every playback address of the installation and writes a replacement
    /// over every occurrence of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The comparison is ordinal and case sensitive, and every occurrence is replaced.</b>
    /// <c>string.Replace(..., StringComparison.Ordinal)</c> is the rule; the repository's SQL
    /// filter only narrows the candidate set. Nothing is trimmed - an operator replacing an
    /// authorization fragment is naming characters, and trimming them would be editing the
    /// request.
    /// </para>
    /// <para>
    /// <b>A refusal is per entity and all-or-nothing.</b> A replacement that would leave any
    /// address of one stream, item or episode blank, or longer than its column, refuses that
    /// entity whole - half a failover chain pointing at the new host and half at the old one
    /// is worse than either. The other entities still change and the request is still a
    /// success. A blank result is checked before an over-long one, so the more fundamental
    /// problem is the one reported.
    /// </para>
    /// <para>
    /// <b>One save for the whole run.</b> The repository stages in-place changes and this class
    /// commits them once, after every entity has been judged; a request that fails validation
    /// stages nothing and returns before the save.
    /// </para>
    /// <para>
    /// <b>Duplicate resulting addresses are allowed.</b> The replacement is a text edit, not a
    /// uniqueness rule: two streams that suddenly share an address are the operator's answer,
    /// and the schema has no constraint that would refuse it.
    /// </para>
    /// </remarks>
    public sealed class SourceReplacementService : ISourceReplacementService
    {
        /// <summary>The sentence a missing or blank search gets.</summary>
        private static readonly string[] FindRule =
        [
            "Enter the text to find in the source addresses."
        ];

        /// <summary>The sentence a replacement identical to the search gets.</summary>
        private static readonly string[] ReplaceRule =
        [
            "The replacement must differ from the text to find."
        ];

        private readonly ISourceReplacementRepository _sources;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceReplacementService"/> class.
        /// </summary>
        /// <param name="sources">Repository holding the candidates and the in-place set intents.</param>
        /// <param name="unitOfWork">Commits everything the run staged, once.</param>
        public SourceReplacementService(
            ISourceReplacementRepository sources,
            IUnitOfWork unitOfWork)
        {
            _sources = sources;
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public async Task<SourceReplacementOutcome> ReplaceAsync(
            ReplaceSourcesOperation operation,
            CancellationToken cancellationToken = default)
        {
            var errors = Validate(operation);

            if (errors.Count > 0)
            {
                // Nothing is read and nothing is staged: a request the two strings made
                // unusable never reaches the tables.
                return new SourceReplacementOutcome
                {
                    Domains = EmptyCounts(),
                    Refusals = [],
                    Errors = errors
                };
            }

            var find = operation.Find!;
            var replace = operation.Replace ?? string.Empty;

            var domains = new List<SourceReplacementDomainCount>(4);
            var refusals = new List<SourceReplacementRefusal>();

            var streamCandidates = await _sources.GetStreamSourceCandidatesAsync(
                find,
                cancellationToken);
            domains.Add(await ApplyToStreamsAsync(
                SourceDomain.VideoStreams,
                streamCandidates,
                find,
                replace,
                VideoStreamLimits.SourceUrlMaxLength,
                refusals,
                cancellationToken));

            var audioCandidates = await _sources.GetAudioSourceCandidatesAsync(
                find,
                cancellationToken);
            domains.Add(await ApplyToStreamsAsync(
                SourceDomain.AudioStreams,
                audioCandidates,
                find,
                replace,
                AudioStreamLimits.SourceUrlMaxLength,
                refusals,
                cancellationToken));

            var vodCandidates = await _sources.GetVodSourceCandidatesAsync(
                find,
                cancellationToken);
            domains.Add(await ApplyToVodAsync(
                vodCandidates,
                find,
                replace,
                refusals,
                cancellationToken));

            var episodeCandidates = await _sources.GetEpisodeSourceCandidatesAsync(
                find,
                cancellationToken);
            domains.Add(await ApplyToEpisodesAsync(
                episodeCandidates,
                find,
                replace,
                refusals,
                cancellationToken));

            var violation = await _unitOfWork.TrySaveAsync(cancellationToken);

            if (violation is not null)
            {
                // No unique index should ever refuse this write - duplicates are allowed and
                // no position or identifier is touched - so a violation here is a bug and is
                // rethrown rather than dressed up as a feature code.
                throw violation;
            }

            return new SourceReplacementOutcome
            {
                Domains = domains,
                Refusals = refusals,
                Errors = null
            };
        }

        /// <summary>Judges the two input strings, reporting every problem at once.</summary>
        private static Dictionary<string, string[]> Validate(ReplaceSourcesOperation operation)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var find = operation.Find;
            var replace = operation.Replace ?? string.Empty;

            var findIsBlank = string.IsNullOrWhiteSpace(find);

            if (findIsBlank)
            {
                errors[nameof(ReplaceSourcesOperation.Find)] = FindRule;
            }

            // Both problems are reported at once, so a request whose search is blank and
            // whose replacement equals it is told about both rather than only the first one
            // a reader would notice. Null and the empty string compare equal here, because
            // the body treats an absent replacement as the empty string.
            if (string.Equals(find ?? string.Empty, replace, StringComparison.Ordinal))
            {
                errors[nameof(ReplaceSourcesOperation.Replace)] = ReplaceRule;
            }

            return errors;
        }

        /// <summary>Replaces the addresses of both stream kinds, one failover chain at a time.</summary>
        private async Task<SourceReplacementDomainCount> ApplyToStreamsAsync<TRow>(
            SourceDomain domain,
            IReadOnlyList<TRow> candidates,
            string find,
            string replace,
            int maxLength,
            List<SourceReplacementRefusal> refusals,
            CancellationToken cancellationToken)
            where TRow : class
        {
            var entities = new List<StreamGroup>();

            foreach (var candidate in candidates)
            {
                var (id, name, position, url) = ReadStreamRow(candidate);

                var group = entities.Find(entry => entry.Id == id);

                if (group is null)
                {
                    group = new StreamGroup(id, name);
                    entities.Add(group);
                }

                group.Urls[position] = url;
            }

            var updated = 0;
            var skipped = 0;

            foreach (var entity in entities)
            {
                var replacements = new SortedDictionary<int, string>();
                var refused = false;

                foreach (var (position, url) in entity.Urls)
                {
                    var next = url.Replace(find, replace, StringComparison.Ordinal);

                    if (string.Equals(next, url, StringComparison.Ordinal))
                    {
                        skipped++;
                        continue;
                    }

                    replacements[position] = next;
                }

                if (replacements.Count == 0)
                {
                    continue;
                }

                if (replacements.Values.Any(string.IsNullOrWhiteSpace))
                {
                    refusals.Add(new SourceReplacementRefusal
                    {
                        Domain = domain,
                        Id = entity.Id,
                        Name = entity.Name,
                        Reason = SourceRefusalReason.Blank
                    });

                    refused = true;
                }
                else if (replacements.Values.Any(url => url.Length > maxLength))
                {
                    refusals.Add(new SourceReplacementRefusal
                    {
                        Domain = domain,
                        Id = entity.Id,
                        Name = entity.Name,
                        Reason = SourceRefusalReason.TooLong
                    });

                    refused = true;
                }

                if (refused)
                {
                    // The addresses that would have changed are not counted as skipped: the
                    // refusal line names the entity, and counting its addresses a second time
                    // would read as "nothing to do" rather than "deliberately not done".
                    continue;
                }

                if (domain is SourceDomain.VideoStreams)
                {
                    await _sources.SetStreamSourcesAsync(entity.Id, replacements, cancellationToken);
                }
                else
                {
                    await _sources.SetAudioSourcesAsync(entity.Id, replacements, cancellationToken);
                }

                updated++;
            }

            return new SourceReplacementDomainCount
            {
                Domain = domain,
                Updated = updated,
                Skipped = skipped
            };
        }

        /// <summary>Replaces the source of every candidate on-demand item.</summary>
        private async Task<SourceReplacementDomainCount> ApplyToVodAsync(
            IReadOnlyList<StoredVodSourceRow> candidates,
            string find,
            string replace,
            List<SourceReplacementRefusal> refusals,
            CancellationToken cancellationToken)
        {
            var updated = 0;
            var skipped = 0;

            foreach (var candidate in candidates)
            {
                var next = candidate.Source.Replace(find, replace, StringComparison.Ordinal);

                if (string.Equals(next, candidate.Source, StringComparison.Ordinal))
                {
                    skipped++;
                    continue;
                }

                var reason = RefusalOf(next, VodLimits.SourceMaxLength);

                if (reason is { } refused)
                {
                    refusals.Add(new SourceReplacementRefusal
                    {
                        Domain = SourceDomain.VideoOnDemand,
                        Id = candidate.Id,
                        Name = candidate.Name,
                        Reason = refused
                    });

                    continue;
                }

                await _sources.SetVodSourceAsync(candidate.Id, next, cancellationToken);
                updated++;
            }

            return new SourceReplacementDomainCount
            {
                Domain = SourceDomain.VideoOnDemand,
                Updated = updated,
                Skipped = skipped
            };
        }

        /// <summary>Replaces the source of every candidate episode.</summary>
        private async Task<SourceReplacementDomainCount> ApplyToEpisodesAsync(
            IReadOnlyList<StoredEpisodeSourceRow> candidates,
            string find,
            string replace,
            List<SourceReplacementRefusal> refusals,
            CancellationToken cancellationToken)
        {
            var updated = 0;
            var skipped = 0;

            foreach (var candidate in candidates)
            {
                var next = candidate.Source.Replace(find, replace, StringComparison.Ordinal);

                if (string.Equals(next, candidate.Source, StringComparison.Ordinal))
                {
                    skipped++;
                    continue;
                }

                var reason = RefusalOf(next, SeriesLimits.SourceMaxLength);

                if (reason is { } refused)
                {
                    refusals.Add(new SourceReplacementRefusal
                    {
                        Domain = SourceDomain.SeriesEpisodes,
                        Id = candidate.Id,
                        Name = candidate.Name,
                        Reason = refused
                    });

                    continue;
                }

                await _sources.SetEpisodeSourceAsync(candidate.Id, next, cancellationToken);
                updated++;
            }

            return new SourceReplacementDomainCount
            {
                Domain = SourceDomain.SeriesEpisodes,
                Updated = updated,
                Skipped = skipped
            };
        }

        /// <summary>Reads the entity, name, position and address out of one of the two source rows.</summary>
        private static (Guid Id, string Name, int Position, string Url) ReadStreamRow<TRow>(TRow row)
            where TRow : class
            => row switch
            {
                StoredStreamSourceRow stream =>
                    (stream.StreamId, stream.StreamName, stream.Position, stream.Url),

                StoredAudioSourceRow audio =>
                    (audio.StreamId, audio.StreamName, audio.Position, audio.Url),

                _ => throw new ArgumentOutOfRangeException(nameof(row))
            };

        /// <summary>
        /// Why one resulting address cannot be stored, or null when it can.
        /// </summary>
        /// <remarks>
        /// Blank first, then length: a value that replaced the whole address with nothing is
        /// the mistake, and its length is not the interesting second fact about it.
        /// </remarks>
        private static SourceRefusalReason? RefusalOf(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return SourceRefusalReason.Blank;
            }

            return value.Length > maxLength ? SourceRefusalReason.TooLong : null;
        }

        /// <summary>The four zero counts a refused request answers with, in the stable order.</summary>
        private static IReadOnlyList<SourceReplacementDomainCount> EmptyCounts() =>
        [
            new() { Domain = SourceDomain.VideoStreams, Updated = 0, Skipped = 0 },
            new() { Domain = SourceDomain.AudioStreams, Updated = 0, Skipped = 0 },
            new() { Domain = SourceDomain.VideoOnDemand, Updated = 0, Skipped = 0 },
            new() { Domain = SourceDomain.SeriesEpisodes, Updated = 0, Skipped = 0 }
        ];

        /// <summary>One stream's candidate addresses, keyed by their position.</summary>
        private sealed class StreamGroup
        {
            public StreamGroup(Guid id, string name)
            {
                Id = id;
                Name = name;
            }

            public Guid Id { get; }

            public string Name { get; }

            public SortedDictionary<int, string> Urls { get; } = [];
        }
    }
}
