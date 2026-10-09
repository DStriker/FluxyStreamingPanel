namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// One row of the admin group list: what the table draws, and nothing it does not.
    /// </summary>
    /// <remarks>
    /// The permissions are a <b>count</b> rather than the set, because the column is a count:
    /// shipping the whole set on every row of every page would answer a question the page
    /// never asks, while the row that needs to <i>show</i> them is the edit form, which fetches
    /// the group in full anyway.
    /// </remarks>
    public sealed record UserGroupListItem
    {
        /// <summary>Identifier of the group.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name, compared case sensitively.</summary>
        public required string Name { get; init; }

        /// <summary>Access level every member holds.</summary>
        public required UserRole Role { get; init; }

        /// <summary>State of the group itself.</summary>
        public required UserStatus Status { get; init; }

        /// <summary>How many permissions this group grants.</summary>
        public required int PermissionsCount { get; init; }

        /// <summary>Whether this group may only be renamed.</summary>
        public required bool IsBase { get; init; }
    }

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
    public sealed record UserGroupDetail
    {
        /// <summary>Identifier of the group. Shown as static text, never as a payload field.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name, compared case sensitively.</summary>
        public required string Name { get; init; }

        /// <summary>Access level every member holds.</summary>
        public required UserRole Role { get; init; }

        /// <summary>State of the group itself.</summary>
        public required UserStatus Status { get; init; }

        /// <summary>Everything this group allows.</summary>
        public required IReadOnlySet<UserPermission> Permissions { get; init; }

        /// <summary>Whether this group may only be renamed.</summary>
        public required bool IsBase { get; init; }

        /// <summary>How many accounts currently belong to this group.</summary>
        public required int Members { get; init; }

        /// <summary>When the row was created.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>When the row last changed.</summary>
        public required DateTimeOffset UpdatedAt { get; init; }
    }
}
