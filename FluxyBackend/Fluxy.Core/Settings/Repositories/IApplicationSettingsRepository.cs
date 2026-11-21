using Fluxy.Core.Settings.Commands;
using Fluxy.Core.Settings.Models;

namespace Fluxy.Core.Settings.Repositories
{
    /// <summary>
    /// Reads and writes the single application settings row.
    /// </summary>
    /// <remarks>
    /// Every method answers the row the installation holds, never a set of rows: the table is
    /// one row by design and the identifier is a constant. A read that finds no row answers
    /// null, and the caller decides - the snapshot provider falls back to the defaults statics
    /// while the seeder creates the row.
    /// </remarks>
    public interface IApplicationSettingsRepository
    {
        /// <summary>Reads the settings row, or null when no row carries the fixed identifier.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<ApplicationSettings?> GetAsync(CancellationToken cancellationToken = default);

        /// <summary>Stages the settings row for insertion.</summary>
        /// <param name="settings">Values the row must carry, including its fixed identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task AddAsync(ApplicationSettings settings, CancellationToken cancellationToken = default);

        /// <summary>Stages a full write of the settings row.</summary>
        /// <param name="settings">Values the row must carry after the write.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>False when no row carries the fixed identifier.</returns>
        Task<bool> UpdateAsync(
            ApplicationSettings settings,
            CancellationToken cancellationToken = default);
    }
}
