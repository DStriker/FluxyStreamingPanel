namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// One row of the admin user list: what the table draws, and nothing the table does not.
    /// </summary>
    /// <remarks>
    /// A projection rather than <see cref="User"/> for the same reason <c>AccountProfile</c> is
    /// one: the business model carries <c>PasswordHash</c> and <c>RegistrationCodeHash</c>, and
    /// a list endpoint would leak them the first time somebody pointed a serializer at the
    /// row it happened to load. Nothing here is a secret, which is exactly why the shape is
    /// stated separately instead of relying on a serializer being configured kindly.
    ///
    /// <see cref="LastSeenAt"/> and <see cref="LastIp"/> describe the same visit: the freshest
    /// refresh token the account holds, revoked or not. A session that was ended still happened,
    /// and a list whose "last visit" only counted live sessions would show a person who was
    /// signed out an hour ago as having been gone for a week.
    /// </remarks>
    public sealed record AdminUser
    {
        /// <summary>Identifier of the account.</summary>
        public required Guid Id { get; init; }

        /// <summary>Login name, at most 20 characters. Unique, compared case sensitively.</summary>
        public required string Username { get; init; }

        /// <summary>Email address of the owner, stored normalized.</summary>
        public required string Email { get; init; }

        /// <summary>Access level of the account, inherited from its group.</summary>
        public required UserRole Role { get; init; }

        /// <summary>Identifier of the group the account belongs to.</summary>
        public required Guid GroupId { get; init; }

        /// <summary>Display name of that group, which is what the column shows.</summary>
        public required string GroupName { get; init; }

        /// <summary>Current state of the account, combining its own row with its group's.</summary>
        public required UserStatus Status { get; init; }

        /// <summary>
        /// When the account last signed in or refreshed, or null when it never has.
        /// </summary>
        public DateTimeOffset? LastSeenAt { get; init; }

        /// <summary>
        /// Address that visit came from, or null when the row recorded none.
        /// </summary>
        public string? LastIp { get; init; }
    }
}
