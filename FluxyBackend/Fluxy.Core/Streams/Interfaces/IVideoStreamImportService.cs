using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Outcomes;

namespace Fluxy.Core.Streams.Interfaces
{
    /// <summary>
    /// Creates streams from the entries of an uploaded M3U playlist.
    /// </summary>
    /// <remarks>
    /// A service of its own rather than a method on <see cref="IVideoStreamService"/>: the
    /// request names a file rather than a row, the answer counts a batch rather than describing
    /// one stream, and a client asking for one of those should not have to know the shape of
    /// the other. The rules it applies to each entry are the same rules a single create uses,
    /// reached through the same helpers.
    /// </remarks>
    public interface IVideoStreamImportService
    {
        /// <summary>
        /// Parses the playlist and writes every entry it can use, reporting the rest.
        /// </summary>
        /// <param name="input">Settings shared by the batch and the text of the file.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The counts, the refusals, or the reason the file could not be read.</returns>
        Task<VideoStreamImportOutcome> ImportAsync(
            NewVideoStreamImport input,
            CancellationToken cancellationToken = default);
    }
}
