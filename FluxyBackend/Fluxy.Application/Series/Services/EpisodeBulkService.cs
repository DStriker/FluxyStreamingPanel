using Fluxy.Core.Series.Commands;
using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Interfaces;
using Fluxy.Core.Series.Outcomes;

namespace Fluxy.Application.Series.Services
{
    /// <summary>
    /// Applies one operation to a set of episodes of one series, one episode at a time.
    /// </summary>
    public sealed class EpisodeBulkService : IEpisodeBulkService
    {
        /// <summary>The sentence a status change without a state gets.</summary>
        private static readonly string[] StatusRule = ["Choose the state to write."];

        /// <summary>The sentence a type change without a destination gets.</summary>
        private static readonly string[] TypeRule =
            ["A bulk type change must choose relay or redirect."];

        private readonly IEpisodeService _episodes;

        /// <summary>
        /// Initializes a new instance of the <see cref="EpisodeBulkService"/> class.
        /// </summary>
        /// <param name="episodes">Service holding the single-row operations.</param>
        public EpisodeBulkService(IEpisodeService episodes)
        {
            _episodes = episodes;
        }

        /// <inheritdoc />
        public async Task<EpisodeBulkOutcome> BulkAsync(
            Guid seriesId,
            BulkEpisodeOperation operation,
            CancellationToken cancellationToken = default)
        {
            if (MissingPayload(operation) is { } missing)
            {
                return RefuseAll(operation, missing.Field, missing.Sentences);
            }

            var items = new List<EpisodeBulkItemResult>(operation.EpisodeIds.Count);

            foreach (var episodeId in operation.EpisodeIds)
            {
                var outcome = operation.Action switch
                {
                    BulkEpisodeAction.ChangeStatus =>
                        await _episodes.UpdateAsync(
                            seriesId,
                            episodeId,
                            new EpisodePatch { Status = operation.Status },
                            cancellationToken),

                    BulkEpisodeAction.ChangeType =>
                        await _episodes.UpdateAsync(
                            seriesId,
                            episodeId,
                            new EpisodePatch { Type = operation.Type },
                            cancellationToken),

                    BulkEpisodeAction.Delete =>
                        await _episodes.DeleteAsync(seriesId, episodeId, cancellationToken),

                    _ => throw new ArgumentOutOfRangeException(
                        nameof(operation),
                        operation.Action,
                        "Unknown bulk episode action.")
                };

                items.Add(new EpisodeBulkItemResult
                {
                    EpisodeId = episodeId,
                    Action = outcome.Action,
                    Errors = outcome.Errors
                });
            }

            return new EpisodeBulkOutcome { Action = operation.Action, Items = items };
        }

        /// <summary>The payload this operation cannot run without, or null when it has one.</summary>
        private static (string Field, string[] Sentences)? MissingPayload(
            BulkEpisodeOperation operation)
        {
            var missing = operation.Action switch
            {
                BulkEpisodeAction.ChangeStatus when operation.Status is null =>
                    (nameof(BulkEpisodeOperation.Status), StatusRule),

                BulkEpisodeAction.ChangeType when operation.Type is null =>
                    (nameof(BulkEpisodeOperation.Type), TypeRule),

                _ => ((string Field, string[] Sentences)?)null
            };

            return missing;
        }

        /// <summary>Answers every named episode with one payload refusal.</summary>
        private static EpisodeBulkOutcome RefuseAll(
            BulkEpisodeOperation operation,
            string field,
            string[] sentences)
            => new()
            {
                Action = operation.Action,
                Items = operation.EpisodeIds
                    .Select(episodeId => new EpisodeBulkItemResult
                    {
                        EpisodeId = episodeId,
                        Action = EpisodeAction.Invalid,
                        Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                        {
                            [field] = sentences
                        }
                    })
                    .ToList()
            };
    }
}
