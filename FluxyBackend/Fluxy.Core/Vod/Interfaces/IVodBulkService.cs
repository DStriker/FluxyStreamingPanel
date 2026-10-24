using Fluxy.Core.Vod.Commands;
using Fluxy.Core.Vod.Outcomes;

namespace Fluxy.Core.Vod.Interfaces
{
    /// <summary>
    /// Applies one operation to a set of on-demand items, one item at a time.
    /// </summary>
    /// <remarks>
    /// The bulk endpoint is not a second implementation of the single-row operations: it is
    /// those operations applied to each row in turn, so a category move that collides with a
    /// name refuses that row alone and the others still move.
    /// </remarks>
    public interface IVodBulkService
    {
        /// <summary>Runs one operation over every item the request names.</summary>
        /// <param name="operation">Which operation, over which items, and the value it writes.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>One entry per identifier, in the order the request named them.</returns>
        Task<BulkVodOutcome> BulkAsync(
            BulkVodOperation operation,
            CancellationToken cancellationToken = default);
    }
}
