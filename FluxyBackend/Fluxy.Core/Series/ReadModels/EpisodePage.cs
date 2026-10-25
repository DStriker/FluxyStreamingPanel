namespace Fluxy.Core.Series.ReadModels
{
    /// <summary>
    /// One page of the episode table of one series, with the count the pager is built from.
    /// </summary>
    public sealed record EpisodePage
    {
        /// <summary>The episodes this page holds, in the requested order.</summary>
        public required IReadOnlyList<EpisodeListItem> Items { get; init; }

        /// <summary>How many episodes the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many episodes one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
