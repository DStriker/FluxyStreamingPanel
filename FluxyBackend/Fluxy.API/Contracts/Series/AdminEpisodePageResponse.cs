namespace Fluxy.API.Contracts.Series
{
    /// <summary>
    /// One page of the episode table of one series.
    /// </summary>
    public sealed record AdminEpisodePageResponse
    {
        /// <summary>The episodes this page holds, in the requested order.</summary>
        public required IReadOnlyList<AdminEpisodeItemResponse> Items { get; init; }

        /// <summary>How many episodes the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many episodes one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
