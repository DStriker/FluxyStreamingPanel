using Fluxy.Core.Series.Commands;
using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Interfaces;
using Fluxy.Core.Series.Outcomes;

namespace Fluxy.Application.Series.Services
{
    /// <summary>
    /// Applies one operation to a set of series, one series at a time.
    /// </summary>
    /// <remarks>
    /// The bulk endpoint is not a second implementation of the single-row operations, it is
    /// those operations applied to each row in turn. A category move is still the update that
    /// knows about the category-scoped name, and a status change is still the update that
    /// validates the whole row; a hundred rows in a request are refused for exactly the reasons
    /// one row is.
    /// </remarks>
    public sealed class SeriesBulkService : ISeriesBulkService
    {
        /// <summary>The sentence a status change without a state gets.</summary>
        private static readonly string[] StatusRule = ["Choose the state to write."];

        /// <summary>The sentence a move without a destination gets.</summary>
        private static readonly string[] CategoryRule =
            ["A bulk move needs the category to move the series to."];

        private readonly ISeriesService _series;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeriesBulkService"/> class.
        /// </summary>
        /// <param name="series">Service holding the single-row operations.</param>
        public SeriesBulkService(ISeriesService series)
        {
            _series = series;
        }

        /// <inheritdoc />
        public async Task<SeriesBulkOutcome> BulkAsync(
            BulkSeriesOperation operation,
            CancellationToken cancellationToken = default)
        {
            if (MissingPayload(operation) is { } missing)
            {
                return RefuseAll(operation, missing.Field, missing.Sentences);
            }

            var items = new List<SeriesBulkItemResult>(operation.SeriesIds.Count);

            foreach (var seriesId in operation.SeriesIds)
            {
                var outcome = operation.Action switch
                {
                    BulkSeriesAction.ChangeStatus =>
                        await _series.UpdateAsync(
                            seriesId,
                            new SeriesPatch { Status = operation.Status },
                            cancellationToken),

                    BulkSeriesAction.MoveCategory =>
                        await _series.UpdateAsync(
                            seriesId,
                            new SeriesPatch { CategoryId = operation.CategoryId },
                            cancellationToken),

                    BulkSeriesAction.Delete =>
                        await _series.DeleteAsync(seriesId, cancellationToken),

                    _ => throw new ArgumentOutOfRangeException(
                        nameof(operation),
                        operation.Action,
                        "Unknown bulk series action.")
                };

                items.Add(new SeriesBulkItemResult
                {
                    SeriesId = seriesId,
                    Action = outcome.Action,
                    Errors = outcome.Errors
                });
            }

            return new SeriesBulkOutcome { Action = operation.Action, Items = items };
        }

        /// <summary>The payload this operation cannot run without, or null when it has one.</summary>
        private static (string Field, string[] Sentences)? MissingPayload(
            BulkSeriesOperation operation)
        {
            var missing = operation.Action switch
            {
                BulkSeriesAction.ChangeStatus when operation.Status is null =>
                    (nameof(BulkSeriesOperation.Status), StatusRule),

                BulkSeriesAction.MoveCategory when operation.CategoryId is null =>
                    (nameof(BulkSeriesOperation.CategoryId), CategoryRule),

                _ => ((string Field, string[] Sentences)?)null
            };

            return missing;
        }

        /// <summary>Answers every named series with one payload refusal.</summary>
        private static SeriesBulkOutcome RefuseAll(
            BulkSeriesOperation operation,
            string field,
            string[] sentences)
            => new()
            {
                Action = operation.Action,
                Items = operation.SeriesIds
                    .Select(seriesId => new SeriesBulkItemResult
                    {
                        SeriesId = seriesId,
                        Action = SeriesAction.Invalid,
                        Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                        {
                            [field] = sentences
                        }
                    })
                    .ToList()
            };
    }
}
