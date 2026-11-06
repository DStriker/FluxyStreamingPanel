namespace Fluxy.Core.Users.ReadModels
{
    /// <summary>
    /// One page of the credit log, with the count the pager is built from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is the size of the filtered list, not of the table, and it is counted
    /// under the very predicate the page is cut from - the reason every filter and the order are
    /// answered by the server.
    /// </remarks>
    public sealed record CreditLogPage
    {
        /// <summary>The entries this page holds, in the requested order.</summary>
        public required IReadOnlyList<CreditLogListItem> Items { get; init; }

        /// <summary>How many entries the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many entries one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
