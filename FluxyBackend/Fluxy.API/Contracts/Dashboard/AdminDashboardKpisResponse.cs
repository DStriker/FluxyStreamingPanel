namespace Fluxy.API.Contracts.Dashboard
{
    /// <summary>
    /// The installation-wide numbers at the top of the dashboard.
    /// </summary>
    /// <remarks>
    /// The field names are the ones the Core read model uses, so the wire shape is a rename of
    /// the service's answer and not a second opinion. The network totals cover the servers that
    /// answered only; <c>serversUnavailable</c> on the parent tells the client the total is
    /// partial.
    /// </remarks>
    public sealed record AdminDashboardKpisResponse
    {
        /// <summary>Every live connection, attributed or not.</summary>
        public required int ActiveConnections { get; init; }

        /// <summary>Distinct accounts with at least one live connection.</summary>
        public required int UsersWithActiveConnections { get; init; }

        /// <summary>Video streams carrying <c>Running</c>.</summary>
        public required int StreamsRunning { get; init; }

        /// <summary>Video streams carrying <c>Stopped</c>.</summary>
        public required int StreamsStopped { get; init; }

        /// <summary>Inbound rate across every available server, in megabits per second.</summary>
        public required double NetworkInMbps { get; init; }

        /// <summary>Outbound rate across every available server, in megabits per second.</summary>
        public required double NetworkOutMbps { get; init; }
    }
}
