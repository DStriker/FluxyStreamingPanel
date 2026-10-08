namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// One page of the admin user list, with the count the pager is built from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is the size of the *filtered* list, not of the table. That is the
    /// whole reason search, filter and order are answered by the server: a total counted over
    /// everything beside rows filtered in the browser gives a pager offering four pages of five
    /// rows when the filter admitted one.
    /// </remarks>
    public sealed record AdminUserPage
    {
        /// <summary>The accounts this page holds, in the requested order.</summary>
        public required IReadOnlyList<AdminUser> Items { get; init; }

        /// <summary>How many accounts the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many accounts one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
