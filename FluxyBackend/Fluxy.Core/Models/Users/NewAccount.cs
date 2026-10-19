namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// Everything the data layer needs to insert an account row.
    /// </summary>
    /// <remarks>
    /// The shape exists because <see cref="User"/> is what a row is <i>read</i> as, and using it
    /// for a write would mean passing values the row does not own yet - an effective status
    /// derived from a group, audit timestamps the context overwrites, a group name that is not a
    /// column. This record names only what is written, which is what lets the repository build
    /// the row without a service ever holding an entity.
    ///
    /// The password and the registration code travel as hashes. Callers hash them before they
    /// get here, because the hash is the part that is stored and clear text has no business
    /// crossing a layer boundary.
    /// </remarks>
    public sealed record NewAccount
    {
        /// <summary>Login name, at most 20 characters. Unique, compared case sensitively.</summary>
        public required string Username { get; init; }

        /// <summary>Email address of the owner. Unique, compared case sensitively.</summary>
        public required string Email { get; init; }

        /// <summary>BCrypt hash of the password.</summary>
        public required string PasswordHash { get; init; }

        /// <summary>Group the account joins, which is where its level and permissions come from.</summary>
        public required Guid GroupId { get; init; }

        /// <summary>State of the account's own row; the effective state also involves the group.</summary>
        public required UserStatus Status { get; init; }

        /// <summary>BCrypt hash of a pending registration code, or null when none is pending.</summary>
        public string? RegistrationCodeHash { get; init; }

        /// <summary>Moment the pending registration code expires, or null when none is pending.</summary>
        public DateTimeOffset? RegistrationCodeExpiresAt { get; init; }

        /// <summary>Moment the account confirmed its address, or null while it is unregistered.</summary>
        public DateTimeOffset? RegisteredAt { get; init; }

        /// <summary>IANA identifier of the display time zone, or null when the browser decides.</summary>
        public string? TimeZone { get; init; }

        /// <summary>Whether a sign-in from a network outside the allow lists is refused.</summary>
        public bool GeoProtectionEnabled { get; init; }

        /// <summary>Whether a session is bound to the address it was opened from.</summary>
        public bool BindSessionToIp { get; init; }
    }
}
