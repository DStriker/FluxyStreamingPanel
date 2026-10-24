namespace Fluxy.Core.Vod.ReadModels
{
    /// <summary>
    /// One page of the on-demand table, with the count the pager is built from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is the size of the filtered list, not of the table. That is the
    /// whole reason search, filter and order are answered by the server.
    /// </remarks>
    public sealed record VodPage
    {
        /// <summary>The items this page holds, in the requested order.</summary>
        public required IReadOnlyList<VodListItem> Items { get; init; }

        /// <summary>How many items the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many items one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
