using Fluxy.Core.Settings.ReadModels;

namespace Fluxy.Core.Settings.Interfaces
{
    /// <summary>
    /// Reads the application settings and writes the whole row on an operator's behalf.
    /// </summary>
    /// <remarks>
    /// The service owns the rules - the trimmed text, every bound, the time zone, the whitelist
    /// and the write-only private key - and the repository owns the one row. Which of its
    /// outcomes becomes a status code is the transport's business.
    /// </remarks>
    public interface ISettingsService
    {
        /// <summary>Reports the current application settings, never the private key.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The settings, or the defaults when the row is absent.</returns>
        Task<ApplicationSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes the whole row and refreshes the snapshot other callers read.
        /// </summary>
        /// <param name="patch">Values the row must carry after the write.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, naming the rejected fields when the input was the problem.</returns>
        Task<Outcomes.SettingsOutcome> UpdateAsync(
            Commands.ApplicationSettingsPatch patch,
            CancellationToken cancellationToken = default);
    }
}
