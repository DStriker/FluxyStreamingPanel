namespace Fluxy.API.Contracts.Tickets
{
    /// <summary>
    /// One page of the support tickets an operator may read, with the count the pager is built
    /// from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is the size of the filtered list after the role scope is applied, not
    /// of the table: the repository counts it under the same predicate the page is cut from, so
    /// the pager never advertises tickets the caller was not going to be shown.
    /// </remarks>
    public sealed record AdminTicketPageResponse
    {
        /// <summary>The tickets this page holds, in the requested order.</summary>
        public required IReadOnlyList<AdminTicketItemResponse> Items { get; init; }

        /// <summary>How many tickets the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many tickets one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
