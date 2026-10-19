namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// The profile page's account fields plus the permissions its group currently grants.
    /// </summary>
    /// <remarks>
    /// The permissions come from the group rows rather than the account, which is why this
    /// shape is read in one query that joins the account to its group and the group's grants.
    /// The service still builds <see cref="AccountProfile"/> from it, so no storage shape
    /// reaches the transport layer.
    /// </remarks>
    public sealed record StoredProfile
    {
        /// <summary>Login name of the account.</summary>
        public required string Username { get; init; }

        /// <summary>Email address the account receives mail at.</summary>
        public required string Email { get; init; }

        /// <summary>Access level inherited from the account's group.</summary>
        public required UserRole Role { get; init; }

        /// <summary>Effective state of the account, combining its row and its group.</summary>
        public required UserStatus Status { get; init; }

        /// <summary>IANA identifier of the chosen display time zone, or null when not set.</summary>
        public string? TimeZone { get; init; }

        /// <summary>Every permission the account's group currently grants.</summary>
        public IReadOnlySet<UserPermission> Permissions { get; init; } =
            new HashSet<UserPermission>();
    }
}
