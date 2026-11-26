namespace Fluxy.Core.Subscriptions.Rows
{
    /// <summary>
    /// How many distinct accounts hold a subscription that names one server as its preferred one.
    /// </summary>
    /// <remarks>
    /// A projection shaped for one query: the dashboard reads the configured side of a server's
    /// load - who was placed there - separately from the log, which answers who is connected
    /// through it right now. A subscription without a preferred server is excluded rather than
    /// grouped under null, because a row that names no server contributes to no server's row.
    /// </remarks>
    public sealed record PreferredServerUserCountRow
    {
        /// <summary>Server the group belongs to; never null.</summary>
        public required Guid ServerId { get; init; }

        /// <summary>How many distinct owners the group's subscriptions belong to.</summary>
        public required int UserCount { get; init; }
    }
}
