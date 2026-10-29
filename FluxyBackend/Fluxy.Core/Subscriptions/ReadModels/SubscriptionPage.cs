namespace Fluxy.Core.Subscriptions.ReadModels
{
    /// <summary>
    /// One page of the subscriptions table, with the count the pager is built from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is the size of the filtered list, not of the table. That is the whole
    /// reason search, the owner/creator/status filters and the order are answered by the server.
    /// </remarks>
    public sealed record SubscriptionPage
    {
        /// <summary>The subscriptions this page holds, in the requested order.</summary>
        public required IReadOnlyList<SubscriptionListItem> Items { get; init; }

        /// <summary>How many subscriptions the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many subscriptions one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
