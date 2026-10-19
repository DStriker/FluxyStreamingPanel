namespace Fluxy.Core.Users.ReadModels
{
    /// <summary>
    /// One page of the admin group list, with the count the pager is built from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is the size of the <i>filtered</i> list, not of the table - the
    /// same reason the users page and the session history both have it: a pager counted over
    /// everything beside rows filtered in the browser offers four pages of five when the
    /// filter admitted one.
    /// </remarks>
    public sealed record UserGroupPage
    {
        /// <summary>The groups this page holds, in the requested order.</summary>
        public required IReadOnlyList<UserGroupListItem> Items { get; init; }

        /// <summary>How many groups the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many groups one page holds.</summary>
        public required int PageSize { get; init; }
    }

    /// <summary>
    /// One group in full: everything the edit form draws.
    /// </summary>
}
