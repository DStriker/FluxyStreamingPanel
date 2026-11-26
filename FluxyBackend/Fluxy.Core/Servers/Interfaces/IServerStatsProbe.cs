using Fluxy.Core.Servers.Models;
using Fluxy.Core.Servers.ReadModels;

namespace Fluxy.Core.Servers.Interfaces
{
    /// <summary>
    /// Reads how one server's machine is doing right now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The contract says nothing about how the reading travels. The one implementation in this
    /// pass asks the server's own HTTP stats endpoint, which is an assumption recorded as A1 in
    /// <c>docs/admin-dashboard.md</c>; a real agent protocol later replaces that adapter and
    /// nothing else.
    /// </para>
    /// <para>
    /// A server that cannot be read is not an error: it is a server the dashboard reports as
    /// unavailable, with zeros in every numeric field.
    /// </para>
    /// </remarks>
    public interface IServerStatsProbe
    {
        /// <summary>Reads one server's statistics.</summary>
        /// <param name="server">Server to read.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The sample, or null when the server could not be read for any reason.</returns>
        Task<ServerStatsSample?> ProbeAsync(Server server, CancellationToken cancellationToken = default);
    }
}
