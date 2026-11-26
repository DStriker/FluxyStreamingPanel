namespace Fluxy.Core.Servers.ReadModels
{
    /// <summary>
    /// One reading of the whole dashboard: the installation KPIs, one row per server, and how
    /// many servers did not answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="ServersUnavailable"/> is what makes a partial network total honest.</b> The
    /// KPI network figures sum the servers that answered; without this count a client could not
    /// tell a quiet installation from one with a machine that stopped reporting.
    /// </para>
    /// <para>
    /// <see cref="GeneratedAt"/> is the instant the snapshot was built, not the instant it was
    /// served - a cached snapshot is deliberately allowed to be a few seconds old.
    /// </para>
    /// </remarks>
    public sealed record DashboardSnapshot
    {
        /// <summary>Installation-wide figures.</summary>
        public required DashboardKpis Kpis { get; init; }

        /// <summary>One row per server row the installation holds, ordered by name.</summary>
        public required IReadOnlyList<DashboardServer> Servers { get; init; }

        /// <summary>How many servers did not answer the probe.</summary>
        public required int ServersUnavailable { get; init; }

        /// <summary>When the snapshot was built.</summary>
        public required DateTimeOffset GeneratedAt { get; init; }
    }
}
