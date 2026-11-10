namespace Fluxy.Core.Requests.ReadModels
{
    /// <summary>
    /// One page of the API request failure log, with the count the pager is built from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is the size of the filtered list, not of the table, and it is counted
    /// under the very predicate the page is cut from - the reason every filter, the search and
    /// the order are answered by the server.
    /// </remarks>
    public sealed record ApiRequestFailurePage
    {
        /// <summary>The records this page holds, in the requested order.</summary>
        public required IReadOnlyList<ApiRequestFailureListItem> Items { get; init; }

        /// <summary>How many records the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many records one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
