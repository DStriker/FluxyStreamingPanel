namespace Fluxy.API.Contracts.Requests
{
    /// <summary>
    /// One page of the API request failure log, with the count the pager is built from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is the size of the filtered list, not of the table: the repository counts
    /// it under the very predicate the page is cut from, before paging, which is what lets the pager
    /// describe the list on screen.
    /// </remarks>
    public sealed record AdminApiRequestFailurePageResponse
    {
        /// <summary>The records this page holds, in the requested order.</summary>
        public required IReadOnlyList<AdminApiRequestFailureItemResponse> Items { get; init; }

        /// <summary>How many records the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many records one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
