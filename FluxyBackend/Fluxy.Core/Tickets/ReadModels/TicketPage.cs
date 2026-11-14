namespace Fluxy.Core.Tickets.ReadModels
{
    /// <summary>
    /// One page of an account's own support tickets, with the count the pager is built from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is the size of the filtered list, not of the table, and it is counted
    /// under the very predicate the page is cut from - the reason the search, the status filter
    /// and the order are answered by the server. The list is already restricted to one account, so
    /// the count is that account's rows and nothing else.
    /// </remarks>
    public sealed record TicketPage
    {
        /// <summary>The tickets this page holds, in the requested order.</summary>
        public required IReadOnlyList<TicketListItem> Items { get; init; }

        /// <summary>How many tickets the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many tickets one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
