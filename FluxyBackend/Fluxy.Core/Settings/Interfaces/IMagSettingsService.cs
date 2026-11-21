using Fluxy.Core.Settings.ReadModels;

namespace Fluxy.Core.Settings.Interfaces
{
    /// <summary>
    /// Reads the MAG portal settings and writes the whole row on an operator's behalf.
    /// </summary>
    /// <remarks>
    /// The service owns the rules; the repository owns the one row. Nothing in this pass consumes
    /// the values - there is no MAG portal producer - so the write exists to store and expose
    /// them.
    /// </remarks>
    public interface IMagSettingsService
    {
        /// <summary>Reports the current MAG settings.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The settings, or the defaults when the row is absent.</returns>
        Task<MagSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes the whole row and refreshes the snapshot other callers read.
        /// </summary>
        /// <param name="patch">Values the row must carry after the write.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, naming the rejected fields when the input was the problem.</returns>
        Task<Outcomes.MagSettingsOutcome> UpdateAsync(
            Commands.MagSettingsPatch patch,
            CancellationToken cancellationToken = default);
    }
}
