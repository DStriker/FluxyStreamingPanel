using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Outcomes;

namespace Fluxy.Core.Streams.Interfaces
{
    /// <summary>
    /// Turns the entries of an uploaded M3U playlist into audio stream rows.
    /// </summary>
    /// <remarks>
    /// The import is collective in the same way the bulk service is - one request, one report
    /// with an entry per line - but its input is a file rather than a set of identifiers, so it
    /// is a contract of its own.
    /// </remarks>
    public interface IAudioStreamImportService
    {
        /// <summary>Reads the playlist and writes the entries it can use.</summary>
        /// <param name="input">The playlist and the settings shared by every entry.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The counts and the lines that did not become streams.</returns>
        Task<AudioStreamImportOutcome> ImportAsync(
            NewAudioStreamImport input,
            CancellationToken cancellationToken = default);
    }
}
