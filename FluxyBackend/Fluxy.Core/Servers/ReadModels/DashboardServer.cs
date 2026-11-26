using Fluxy.Core.Servers.Enums;

namespace Fluxy.Core.Servers.ReadModels
{
    /// <summary>
    /// One installation server as the dashboard draws it: what was configured, how many
    /// connections and viewers it carries, and how its machine is doing right now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every server row is returned, disabled ones included.</b> A disabled server is a fact
    /// the operator configured; the dashboard labels it with <see cref="Status"/> rather than
    /// hiding it, so the table on screen matches the servers table.
    /// </para>
    /// <para>
    /// <b>A server whose machine did not answer is still a row.</b> <see cref="Available"/> is
    /// false and every numeric telemetry field is zero, which is the shape a client draws as "no
    /// answer" rather than as an idle machine.
    /// </para>
    /// <para>
    /// <b><see cref="UsersWithActiveConnections"/> uses the real <c>server_id</c> only.</b>
    /// Connections whose log row carries no server - the unattributed ones - count in the
    /// installation totals and contribute to no server row. The per-server figure therefore
    /// becomes accurate as the capture path starts writing <c>server_id</c>; before that, the
    /// difference between the installation figure and the sum of the rows is exactly the
    /// unattributed tail.
    /// </para>
    /// </remarks>
    public sealed record DashboardServer
    {
        /// <summary>Identifier of the server.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the server is active or disabled.</summary>
        public required ServerStatus Status { get; init; }

        /// <summary>Whether the machine answered the probe.</summary>
        public required bool Available { get; init; }

        /// <summary>Live connections attributed to this server.</summary>
        public required int ActiveConnections { get; init; }

        /// <summary>Distinct accounts whose subscription names this server as its preferred one.</summary>
        public required int PreferredServerUsers { get; init; }

        /// <summary>
        /// Distinct accounts with a live connection attributed to this server - the real
        /// <c>server_id</c> only. See the remarks on the type for the attribution rule.
        /// </summary>
        public required int UsersWithActiveConnections { get; init; }

        /// <summary>How long the machine has been up, in seconds; zero when unavailable.</summary>
        public required long UptimeSeconds { get; init; }

        /// <summary>Processor load as a percentage; zero when unavailable.</summary>
        public required double CpuPercent { get; init; }

        /// <summary>Memory in use as a percentage; zero when unavailable.</summary>
        public required double RamPercent { get; init; }

        /// <summary>Disk in use as a percentage; zero when unavailable.</summary>
        public required double DiskPercent { get; init; }

        /// <summary>Inbound rate in megabits per second; zero when unavailable.</summary>
        public required double NetworkInMbps { get; init; }

        /// <summary>Outbound rate in megabits per second; zero when unavailable.</summary>
        public required double NetworkOutMbps { get; init; }
    }
}
