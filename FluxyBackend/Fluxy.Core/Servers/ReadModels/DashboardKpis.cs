namespace Fluxy.Core.Servers.ReadModels
{
    /// <summary>
    /// The installation-wide numbers at the top of the dashboard.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ActiveConnections"/> counts every live connection log row, including the rows no
    /// server could be attributed to; <see cref="UsersWithActiveConnections"/> counts the distinct
    /// accounts behind them. The per-server figures below do not add up to these numbers on
    /// purpose - see <c>DashboardServer.UsersWithActiveConnections</c>.
    /// </para>
    /// <para>
    /// <see cref="NetworkInMbps"/> and <see cref="NetworkOutMbps"/> sum the servers that answered
    /// only. <c>DashboardSnapshot.ServersUnavailable</c> is what lets a client label the total as
    /// partial rather than wrong.
    /// </para>
    /// </remarks>
    public sealed record DashboardKpis
    {
        /// <summary>Every live connection, attributed or not.</summary>
        public required int ActiveConnections { get; init; }

        /// <summary>Distinct accounts with at least one live connection.</summary>
        public required int UsersWithActiveConnections { get; init; }

        /// <summary>Video streams carrying <c>VideoStreamStatus.Running</c>.</summary>
        public required int StreamsRunning { get; init; }

        /// <summary>Video streams carrying <c>VideoStreamStatus.Stopped</c>.</summary>
        public required int StreamsStopped { get; init; }

        /// <summary>Inbound rate across every available server, in megabits per second.</summary>
        public required double NetworkInMbps { get; init; }

        /// <summary>Outbound rate across every available server, in megabits per second.</summary>
        public required double NetworkOutMbps { get; init; }
    }
}
