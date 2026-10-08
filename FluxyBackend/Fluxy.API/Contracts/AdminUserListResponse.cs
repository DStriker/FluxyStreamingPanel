namespace Fluxy.API.Contracts
{
    /// <summary>
    /// One row of the admin user table: the eight things a line of it draws.
    /// </summary>
    /// <remarks>
    /// Role and status travel as the name of the enum member, spelled exactly as the profile
    /// spells its role - <c>Admin</c>, <c>Blocked</c> - because that is what a client already
    /// matches on and a second spelling for the same enum would be a second fact about it.
    ///
    /// <see cref="LastSeenAt"/> and <see cref="LastIp"/> are one visit, not two columns: the
    /// address belongs to that moment, and both are null for an account that has never signed
    /// in. Both are read server side for the same reason the session history resolves its
    /// GeoIP there - the page reads what it is given.
    /// </remarks>
    public sealed record AdminUserItemResponse
    {
        /// <summary>Identifier of the account, as text. The table shows it shortened.</summary>
        public required string Id { get; init; }

        /// <summary>Login name, at most 20 characters. Unique, compared case sensitively.</summary>
        public required string Username { get; init; }

        /// <summary>Email address of the owner, stored normalized.</summary>
        public required string Email { get; init; }

        /// <summary>Access level, as the name of the enum member.</summary>
        public required string Role { get; init; }

        /// <summary>State of the account, as the name of the enum member.</summary>
        public required string Status { get; init; }

        /// <summary>When the account last signed in or refreshed, or null when it never has.</summary>
        public DateTimeOffset? LastSeenAt { get; init; }

        /// <summary>Address that visit came from, or null when the row recorded none.</summary>
        public string? LastIp { get; init; }
    }

    /// <summary>
    /// One page of the admin user table, with the count its pager is built from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is the size of the *filtered* list. Search, filter and order are
    /// answered by the server for exactly this number: a total counted over everything beside
    /// rows filtered in the browser gives a pager offering four pages of five rows when the
    /// filter admitted one.
    /// </remarks>
    public sealed record AdminUserListResponse
    {
        /// <summary>The accounts this page holds, in the requested order.</summary>
        public required IReadOnlyList<AdminUserItemResponse> Items { get; init; }

        /// <summary>How many accounts the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many accounts one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
