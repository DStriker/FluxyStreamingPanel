namespace Fluxy.API.Contracts.Dashboard
{
    /// <summary>
    /// The admin dashboard: installation KPIs, one row per server, and how many servers did not
    /// answer.
    /// </summary>
    /// <remarks>
    /// <c>serversUnavailable</c> is what lets a client label the network totals as partial - they
    /// sum the servers that answered only. <c>generatedAt</c> is when the snapshot was built, not
    /// when it was served: the snapshot is cached for a few seconds on purpose.
    /// </remarks>
    public sealed record AdminDashboardResponse
    {
        /// <summary>Installation-wide figures.</summary>
        public required AdminDashboardKpisResponse Kpis { get; init; }

        /// <summary>One row per server, ordered by name.</summary>
        public required IReadOnlyList<AdminDashboardServerResponse> Servers { get; init; }

        /// <summary>How many servers did not answer the probe.</summary>
        public required int ServersUnavailable { get; init; }

        /// <summary>When the snapshot was built.</summary>
        public required DateTimeOffset GeneratedAt { get; init; }
    }
}
