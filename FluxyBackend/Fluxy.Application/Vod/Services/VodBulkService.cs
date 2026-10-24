using Fluxy.Core.Vod.Commands;
using Fluxy.Core.Vod.Enums;
using Fluxy.Core.Vod.Interfaces;
using Fluxy.Core.Vod.Outcomes;

namespace Fluxy.Application.Vod.Services
{
    /// <summary>
    /// Applies one operation to a set of on-demand items, one item at a time.
    /// </summary>
    /// <remarks>
    /// The bulk endpoint is not a second implementation of the single-row operations, it is
    /// those operations applied to each row in turn. A category move is still the update that
    /// knows about the category-scoped name, and a status change is still the update that
    /// validates the whole row; a hundred rows in a request are refused for exactly the reasons
    /// one row is.
    /// </remarks>
    public sealed class VodBulkService : IVodBulkService
    {
        /// <summary>The sentence a status change without a state gets.</summary>
        private static readonly string[] StatusRule = ["Choose the state to write."];

        /// <summary>The sentence a move without a destination gets.</summary>
        private static readonly string[] CategoryRule =
            ["A bulk move needs the category to move the videos to."];

        /// <summary>The sentence a type change without a destination gets.</summary>
        private static readonly string[] TypeRule =
            ["A bulk type change must choose relay or redirect."];

        private readonly IVodService _items;

        /// <summary>
        /// Initializes a new instance of the <see cref="VodBulkService"/> class.
        /// </summary>
        /// <param name="items">Service holding the single-item operations.</param>
        public VodBulkService(IVodService items)
        {
            _items = items;
        }

        /// <inheritdoc />
        public async Task<BulkVodOutcome> BulkAsync(
            BulkVodOperation operation,
            CancellationToken cancellationToken = default)
        {
            // A payload an operation needs is refused for every row rather than ignored. The
            // transport refuses the same mistakes with a 400 before any row is reached; if one
            // ever arrives here, every item is answered with the field that was missing instead
            // of the run reporting success over rows it never changed.
            if (MissingPayload(operation) is { } missing)
            {
                return RefuseAll(operation, missing.Field, missing.Sentences);
            }

            var items = new List<BulkVodItemResult>(operation.VodIds.Count);

            foreach (var vodId in operation.VodIds)
            {
                var outcome = operation.Action switch
                {
                    BulkVodAction.ChangeStatus =>
                        await _items.UpdateAsync(
                            vodId,
                            new VodItemPatch { Status = operation.Status },
                            cancellationToken),

                    BulkVodAction.MoveCategory =>
                        await _items.UpdateAsync(
                            vodId,
                            new VodItemPatch { CategoryId = operation.CategoryId },
                            cancellationToken),

                    BulkVodAction.ChangeType =>
                        await _items.UpdateAsync(
                            vodId,
                            new VodItemPatch { Type = operation.Type },
                            cancellationToken),

                    BulkVodAction.Delete =>
                        await _items.DeleteAsync(vodId, cancellationToken),

                    // Unreachable: the enum holds no other member and the transport refuses a
                    // name that maps to none. Thrown rather than skipped, because an entry
                    // missing from the answer is an item the client believes it acted on.
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(operation),
                        operation.Action,
                        "Unknown bulk on-demand action.")
                };

                items.Add(new BulkVodItemResult
                {
                    VodId = vodId,
                    Action = outcome.Action,
                    Errors = outcome.Errors
                });
            }

            return new BulkVodOutcome { Action = operation.Action, Items = items };
        }

        /// <summary>The payload this operation cannot run without, or null when it has one.</summary>
        private static (string Field, string[] Sentences)? MissingPayload(BulkVodOperation operation)
        {
            var missing = operation.Action switch
            {
                BulkVodAction.ChangeStatus when operation.Status is null =>
                    (nameof(BulkVodOperation.Status), StatusRule),

                BulkVodAction.MoveCategory when operation.CategoryId is null =>
                    (nameof(BulkVodOperation.CategoryId), CategoryRule),

                BulkVodAction.ChangeType when operation.Type is null =>
                    (nameof(BulkVodOperation.Type), TypeRule),

                _ => ((string Field, string[] Sentences)?)null
            };

            return missing;
        }

        /// <summary>Answers every named item with one payload refusal.</summary>
        private static BulkVodOutcome RefuseAll(
            BulkVodOperation operation,
            string field,
            string[] sentences)
            => new()
            {
                Action = operation.Action,
                Items = operation.VodIds
                    .Select(vodId => new BulkVodItemResult
                    {
                        VodId = vodId,
                        Action = VodAction.Invalid,
                        Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                        {
                            [field] = sentences
                        }
                    })
                    .ToList()
            };
    }
}
