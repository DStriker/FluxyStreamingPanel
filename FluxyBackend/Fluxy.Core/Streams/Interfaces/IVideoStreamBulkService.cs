using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Outcomes;

namespace Fluxy.Core.Streams.Interfaces
{
    /// <summary>
    /// Applies one operation to a set of streams and reports how each one ended.
    /// </summary>
    /// <remarks>
    /// Deliberately a service of its own rather than another method on
    /// <see cref="IVideoStreamService"/>. The single-row operations and the collective one
    /// answer different questions: one names a row and ends in one outcome, the other names a
    /// set a table assembled on screen and ends in one entry per row. The bulk service is an
    /// orchestration over the single-row methods, not a second implementation of their rules.
    /// </remarks>
    public interface IVideoStreamBulkService
    {
        /// <summary>
        /// Applies one operation to every stream the request names, and reports how each one
        /// ended.
        /// </summary>
        /// <param name="operation">Which operation, over which streams, and the value it writes.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// One entry per named stream, in the order the operation named them, each carrying the
        /// very <c>VideoStreamAction</c> the corresponding single-row method returned.
        /// </returns>
        Task<BulkVideoStreamOutcome> BulkAsync(
            BulkVideoStreamOperation operation,
            CancellationToken cancellationToken = default);
    }
}
