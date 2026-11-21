using Fluxy.Core.Settings.Models;

namespace Fluxy.Core.Settings.Repositories
{
    /// <summary>
    /// Reads and writes the single MAG settings row.
    /// </summary>
    /// <remarks>
    /// The shape is the application settings repository's: one row, a fixed identifier, and a
    /// null read when the row is absent so the caller can fall back to the defaults statics.
    /// </remarks>
    public interface IMagSettingsRepository
    {
        /// <summary>Reads the MAG settings row, or null when no row carries the fixed identifier.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<MagSettings?> GetAsync(CancellationToken cancellationToken = default);

        /// <summary>Stages the MAG settings row for insertion.</summary>
        /// <param name="settings">Values the row must carry, including its fixed identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task AddAsync(MagSettings settings, CancellationToken cancellationToken = default);

        /// <summary>Stages a full write of the MAG settings row.</summary>
        /// <param name="settings">Values the row must carry after the write.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>False when no row carries the fixed identifier.</returns>
        Task<bool> UpdateAsync(
            MagSettings settings,
            CancellationToken cancellationToken = default);
    }
}
