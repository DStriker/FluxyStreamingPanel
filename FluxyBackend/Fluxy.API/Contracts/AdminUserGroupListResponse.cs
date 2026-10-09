namespace Fluxy.API.Contracts
{
    /// <summary>
    /// One row of the admin group table: the six things a line of it draws.
    /// </summary>
    /// <remarks>
    /// Role and status travel as the name of the enum member, spelled exactly as the account
    /// table spells its own pair - <c>Admin</c>, <c>Blocked</c> - because that is what a client
    /// already matches on and a second spelling for the same enums would be a second fact
    /// about them.
    ///
    /// The permissions are a count rather than the set, for the same reason the model says so:
    /// the column is a count, and shipping the whole set on every row of every page would
    /// answer a question the page never asks. The set lives on the detail, whose page is the
    /// edit form.
    /// </remarks>
    public sealed record AdminUserGroupItemResponse
    {
        /// <summary>Identifier of the group, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Display name, unique, compared case sensitively.</summary>
        public required string Name { get; init; }

        /// <summary>Access level every member inherits, as the name of the enum member.</summary>
        public required string Role { get; init; }

        /// <summary>State of the group itself, as the name of the enum member.</summary>
        public required string Status { get; init; }

        /// <summary>How many permissions this group grants.</summary>
        public required int PermissionsCount { get; init; }

        /// <summary>Whether this group may only be renamed.</summary>
        public required bool IsBase { get; init; }
    }

    /// <summary>
    /// One page of the admin group table, with the count its pager is built from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is the size of the *filtered* list. Search, filter and order are
    /// answered by the server for exactly this number: a total counted over everything beside
    /// rows filtered in the browser gives a pager offering four pages of five groups when the
    /// filter admitted one.
    /// </remarks>
    public sealed record AdminUserGroupListResponse
    {
        /// <summary>The groups this page holds, in the requested order.</summary>
        public required IReadOnlyList<AdminUserGroupItemResponse> Items { get; init; }

        /// <summary>How many groups the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many groups one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
