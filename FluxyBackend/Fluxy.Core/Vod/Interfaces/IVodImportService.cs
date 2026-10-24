using Fluxy.Core.Vod.Commands;
using Fluxy.Core.Vod.Outcomes;

namespace Fluxy.Core.Vod.Interfaces
{
    /// <summary>
    /// Turns the entries of an uploaded M3U playlist into on-demand items.
    /// </summary>
    /// <remarks>
    /// The import is a partial success by design: a hand-made file has entries that are not
    /// absolute URLs, titles that are too long, and names already used in the category, and
    /// none of those is a reason to discard the other four thousand entries.
    /// </remarks>
    public interface IVodImportService
    {
        /// <summary>Reads a playlist and writes every entry it can use.</summary>
        /// <param name="input">The file and the settings shared by the batch.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The counts, and one problem per entry that did not land.</returns>
        Task<VodImportOutcome> ImportAsync(
            NewVodImport input,
            CancellationToken cancellationToken = default);
    }
}
