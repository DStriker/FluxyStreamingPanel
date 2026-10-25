namespace Fluxy.API.Contracts.Series
{
    /// <summary>
    /// One page of the admin series table, with the count the pager is built from.
    /// </summary>
    public sealed record AdminSeriesPageResponse
    {
        /// <summary>The series this page holds, in the requested order.</summary>
        public required IReadOnlyList<AdminSeriesItemResponse> Items { get; init; }

        /// <summary>How many series the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many series one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
