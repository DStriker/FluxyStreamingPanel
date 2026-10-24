namespace Fluxy.API.Contracts.Vod
{
    /// <summary>
    /// One page of the admin on-demand table, with the count the pager is built from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is the size of the filtered list, not of the table. That is the
    /// whole reason search, filters, order and paging are answered by the server.
    /// </remarks>
    public sealed record AdminVodPageResponse
    {
        /// <summary>The videos this page holds, in the requested order.</summary>
        public required IReadOnlyList<AdminVodItemResponse> Items { get; init; }

        /// <summary>How many videos the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many videos one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
