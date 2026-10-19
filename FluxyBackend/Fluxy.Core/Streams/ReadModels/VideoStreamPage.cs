namespace Fluxy.Core.Streams.ReadModels
{
    /// <summary>
    /// One page of the streams table, with the count the pager is built from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is the size of the *filtered* list, not of the table. That is the
    /// whole reason search, filter and order are answered by the server: a total counted over
    /// everything beside rows filtered in the browser gives a pager offering four pages of five
    /// rows when the filter admitted one.
    /// </remarks>
    public sealed record VideoStreamPage
    {
        /// <summary>The streams this page holds, in the requested order.</summary>
        public required IReadOnlyList<VideoStreamListItem> Items { get; init; }

        /// <summary>How many streams the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many streams one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
