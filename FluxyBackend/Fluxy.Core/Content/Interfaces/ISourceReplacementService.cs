using Fluxy.Core.Content.Commands;
using Fluxy.Core.Content.Outcomes;

namespace Fluxy.Core.Content.Interfaces
{
    /// <summary>
    /// The mass source replacement: one find string, one replacement, every playback address.
    /// </summary>
    /// <remarks>
    /// <b>The scope is playback addresses only</b> - a video stream's sources, an audio
    /// stream's sources, an on-demand item's source and an episode's source. Logos, posters,
    /// backdrops, subtitle addresses, stored file paths and the reported active source are
    /// deliberately outside it: swapping a host is a question about where a medium is played
    /// from, not about every address the row happens to store.
    /// </remarks>
    public interface ISourceReplacementService
    {
        /// <summary>
        /// Replaces every occurrence of the find string in every playback address.
        /// </summary>
        /// <param name="operation">The two strings the operator supplied.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// One count per domain and the entities the run declined to touch. The input strings
        /// are refused by the outcome's errors rather than by an exception, because a usable
        /// request is still a partial-success answer.
        /// </returns>
        Task<SourceReplacementOutcome> ReplaceAsync(
            ReplaceSourcesOperation operation,
            CancellationToken cancellationToken = default);
    }
}
