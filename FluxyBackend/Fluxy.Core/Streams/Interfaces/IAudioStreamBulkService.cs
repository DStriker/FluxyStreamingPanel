using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Outcomes;

namespace Fluxy.Core.Streams.Interfaces
{
    /// <summary>
    /// Applies one collective operation to a set of audio streams.
    /// </summary>
    /// <remarks>
    /// A bulk request names a set the table assembled on screen and answers with one entry per
    /// identifier, so it is a service of its own rather than a loop in the controller. The
    /// operations themselves are the single-row service's: a move is its update, a start is its
    /// transition, and a delete is its removal.
    /// </remarks>
    public interface IAudioStreamBulkService
    {
        /// <summary>Applies <paramref name="operation"/> to every stream it names.</summary>
        /// <param name="operation">The operation and the rows it acts on.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>One entry per identifier, in the order the request named them.</returns>
        Task<BulkAudioStreamOutcome> BulkAsync(
            BulkAudioStreamOperation operation,
            CancellationToken cancellationToken = default);
    }
}
