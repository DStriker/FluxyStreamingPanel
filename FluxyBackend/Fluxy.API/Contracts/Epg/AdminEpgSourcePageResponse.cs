namespace Fluxy.API.Contracts.Epg
{
    /// <summary>
    /// One page of the guide source table, with the count the pager is built from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is the size of the filtered list, not of the table. That is the
    /// whole reason search and order are answered by the server.
    /// </remarks>
    public sealed record AdminEpgSourcePageResponse
    {
        /// <summary>The sources this page holds, in the requested order.</summary>
        public required IReadOnlyList<AdminEpgSourceResponse> Items { get; init; }

        /// <summary>How many sources the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many sources one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
