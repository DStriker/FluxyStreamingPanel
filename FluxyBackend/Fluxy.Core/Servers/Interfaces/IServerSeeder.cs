using Fluxy.Core.Servers.Models;

namespace Fluxy.Core.Servers.Interfaces
{
    /// <summary>
    /// Makes sure the installation has its main server row.
    /// </summary>
    /// <remarks>
    /// The row is a runtime fact rather than a schema one: the address, the port and the
    /// operating system of the machine are only known once the process is running, which is why
    /// the seeder writes it on startup instead of a migration inserting it. It is idempotent -
    /// a row that already exists is left exactly as the operator left it.
    /// </remarks>
    public interface IServerSeeder
    {
        /// <summary>
        /// Creates the main server when the installation has none, and does nothing on every
        /// later run.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SeedAsync(CancellationToken cancellationToken = default);
    }
}
