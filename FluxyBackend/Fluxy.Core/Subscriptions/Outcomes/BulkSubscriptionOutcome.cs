using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.Outcomes
{
    /// <summary>
    /// The outcome of a bulk run: which operation it was, and how each row ended.
    /// </summary>
    public sealed record BulkSubscriptionOutcome
    {
        /// <summary>Which operation was run.</summary>
        public required BulkSubscriptionAction Action { get; init; }

        /// <summary>One entry per identifier, in the order it was named.</summary>
        public required IReadOnlyList<BulkSubscriptionItemResult> Items { get; init; }
    }
}
