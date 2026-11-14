namespace Fluxy.Core.Tickets.ReadModels
{
    /// <summary>
    /// One page of the support tickets an operator may read, with the count the pager is built
    /// from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is counted after the role scope is applied and under the same predicate
    /// the page is cut from, so it never advertises pages whose tickets the operator was never
    /// going to be shown.
    /// </remarks>
    public sealed record AdminTicketPage
    {
        /// <summary>The tickets this page holds, in the requested order.</summary>
        public required IReadOnlyList<AdminTicketListItem> Items { get; init; }

        /// <summary>How many tickets the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many tickets one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
