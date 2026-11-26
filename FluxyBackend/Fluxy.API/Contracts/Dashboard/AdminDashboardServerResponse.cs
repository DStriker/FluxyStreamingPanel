namespace Fluxy.API.Contracts.Dashboard
{
    /// <summary>
    /// One installation server as the dashboard draws it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every server row is answered, disabled ones included. <c>status</c> is the enum member's
    /// name (<c>Active</c> / <c>Disabled</c>), the spelling the server responses use.
    /// </para>
    /// <para>
    /// <c>available</c> is false when the machine did not answer the probe, and every numeric
    /// telemetry field is then zero. A client draws that as "no answer" rather than as an idle
    /// machine.
    /// </para>
    /// <para>
    /// <c>usersWithActiveConnections</c> counts the real <c>server_id</c> only: connections with
    /// no server are in the installation totals and in no server row.
    /// </para>
    /// </remarks>
    public sealed record AdminDashboardServerResponse
    {
        /// <summary>Identifier of the server, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Display name.</summary>
        public required string Name { get; init; }

        /// <summary>State of the server, as the name of the enum member.</summary>
        public required string Status { get; init; }

        /// <summary>Whether the machine answered the probe.</summary>
        public required bool Available { get; init; }

        /// <summary>Live connections attributed to this server.</summary>
        public required int ActiveConnections { get; init; }

        /// <summary>Distinct accounts whose subscription names this server as its preferred one.</summary>
        public required int PreferredServerUsers { get; init; }

        /// <summary>Distinct accounts with a live connection attributed to this server.</summary>
        public required int UsersWithActiveConnections { get; init; }

        /// <summary>Uptime in seconds; zero when unavailable.</summary>
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
