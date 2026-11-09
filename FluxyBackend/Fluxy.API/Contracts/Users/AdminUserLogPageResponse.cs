namespace Fluxy.API.Contracts.Users
{
    /// <summary>
    /// One page of the admin user audit log, with the count the pager is built from.
    /// </summary>
    public sealed record AdminUserLogPageResponse
    {
        /// <summary>The entries this page holds, in the requested order.</summary>
        public required IReadOnlyList<AdminUserLogItemResponse> Items { get; init; }

        /// <summary>
        /// How many entries the same filter admits, all pages together.
        /// </summary>
        /// <remarks>
        /// The size of the filtered list, not of the table: it is counted under the very predicate
        /// the page is cut from, which is what lets the pager describe the list on screen.
        /// </remarks>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many entries one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
