using Fluxy.Core.Servers.ReadModels;

namespace Fluxy.Core.Servers.Interfaces
{
    /// <summary>
    /// Builds the dashboard reading: installation KPIs plus one row per server.
    /// </summary>
    /// <remarks>
    /// The service owns the composition - which grouped read becomes which figure, and how a
    /// server that did not answer is drawn - while the transport owns the gate and the wire
    /// shape. The per-server telemetry is probed live; the rest is read from the database.
    /// </remarks>
    public interface IDashboardService
    {
        /// <summary>Reads the dashboard, from the cache when one is available.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<DashboardSnapshot> GetAsync(CancellationToken cancellationToken = default);
    }
}
