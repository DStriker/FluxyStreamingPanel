namespace Fluxy.Core.Subscriptions.Utils
{
    /// <summary>
    /// Bounds of the values a tariff plan row stores, and of the list endpoint that reads it.
    /// </summary>
    /// <remarks>
    /// The limits live in this layer rather than beside the table configuration so a service can
    /// judge a request without referencing the data access assembly. The configuration uses the
    /// same constants, so a column width and the rule that refuses an oversized value cannot
    /// drift apart. Only the bounds the domain states are here: a price, a connection count and
    /// the trial ceiling. The length itself has a floor and no ceiling in its own unit - "10
    /// years" is a number and a unit, not a number of days to clamp.
    /// </remarks>
    public static class TariffPlanLimits
    {
        /// <summary>Longest display name a plan may carry.</summary>
        public const int NameMaxLength = 100;

        /// <summary>Cheapest plan, in credits. Zero is a free plan rather than a missing price.</summary>
        public const int CostMin = 0;

        /// <summary>Shortest validity period a plan may name.</summary>
        public const int DurationValueMin = 1;

        /// <summary>Fewest simultaneous connections a plan may allow.</summary>
        public const int ConnectionsMin = 1;

        /// <summary>Most simultaneous connections a plan may allow.</summary>
        public const int ConnectionsMax = 10000;

        /// <summary>
        /// Longest validity a <b>trial</b> plan may name, in hours. Thirty days is 720 hours,
        /// which is the whole rule: a trial is not a cheap month.
        /// </summary>
        public const int TrialMaxHours = 30 * 24;

        /// <summary>
        /// Longest validity a <b>trial</b> plan may name, in days. The same thirty days stated in
        /// the unit an operator is most likely to type.
        /// </summary>
        public const int TrialMaxDays = 30;

        /// <summary>Items one page holds when the caller names no size.</summary>
        public const int DefaultPageSize = 20;

        /// <summary>Most items one page may hold.</summary>
        public const int MaxPageSize = 1000;

        /// <summary>Most characters one search may carry.</summary>
        public const int MaxSearchLength = 200;
    }
}
