namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// Immutable business representation of a user account.
    /// </summary>
    /// <remarks>
    /// The record deliberately carries <see cref="PasswordHash"/> and
    /// <see cref="RegistrationCodeHash"/>, because they are part of the state of the account
    /// and the authentication code needs them. It is not a response contract: anything sent
    /// to a client has to be built from this model explicitly, without those two properties.
    /// </remarks>
    public sealed record User
    {
        /// <summary>Identifier of the account.</summary>
        public required Guid Id { get; init; }

        /// <summary>Login name, at most 20 characters. Unique, compared case sensitively.</summary>
        public required string Username { get; init; }

        /// <summary>Email address of the owner. Unique, compared case sensitively.</summary>
        public required string Email { get; init; }

        /// <summary>BCrypt hash of the password. Never returned to a client.</summary>
        public required string PasswordHash { get; init; }

        /// <summary>
        /// Identifier of the group the account belongs to, and therefore the fact that
        /// replaces a role column on the row.
        /// </summary>
        /// <remarks>
        /// Every account is in exactly one group, and every account that has left the
        /// installation through registration has been given one, so this is required rather
        /// than nullable: a row without a group would be an account whose level and
        /// permissions nobody can answer for.
        /// </remarks>
        public required Guid GroupId { get; init; }

        /// <summary>
        /// Name of that group, for the one place that shows an account's own group beside it.
        /// </summary>
        public required string GroupName { get; init; }

        /// <summary>
        /// Access level the account holds <b>now</b>, inherited from
        /// <see cref="GroupId"/>'s role rather than stored on the account.
        /// </summary>
        /// <remarks>
        /// The column it used to live on is gone. Anything that writes this property is
        /// wrong - the account stores a group, and the level is a fact about the group.
        /// </remarks>
        public UserRole Role { get; init; } = UserRole.Client;

        /// <summary>
        /// Current state of the account, combining its own row with its group's; see
        /// <see cref="UserStatusComposition"/>.
        /// </summary>
        public UserStatus Status { get; init; } = UserStatus.Unregistered;

        /// <summary>
        /// BCrypt hash of the one-time code sent to <see cref="Email"/> during registration,
        /// or null when no code is pending.
        /// </summary>
        public string? RegistrationCodeHash { get; init; }

        /// <summary>
        /// Moment the pending one-time code stops being accepted, or null when no code is
        /// pending. Issuing a new code replaces the previous one.
        /// </summary>
        public DateTimeOffset? RegistrationCodeExpiresAt { get; init; }

        /// <summary>
        /// Moment the account confirmed its email address, or null while it is
        /// <see cref="UserStatus.Unregistered"/>. It is a timestamp only: the state itself
        /// lives in <see cref="Status"/>, and blocking an account does not clear it.
        /// </summary>
        public DateTimeOffset? RegisteredAt { get; init; }

        /// <summary>
        /// IANA identifier of the display time zone, or null when the visitor has not chosen
        /// one and it should be read from their browser.
        /// </summary>        /// <remarks>
        /// A display preference, not a claim of the token: like <see cref="Email"/> it is
        /// deliberately absent from the access token, so a change reaches the page on the next
        /// read rather than at the moment the token expires. The value is an IANA name
        /// (`Europe/Moscow`), never a Windows one and never a bare UTC offset, because a
        /// product of this string with `TimeZoneInfo` has to survive both Linux and Windows
        /// and has to know about daylight saving. Null means "not set" and is a valid state,
        /// not an error: it is what every account has until somebody picks a zone.
        /// </remarks>
        public string? TimeZone { get; init; }

        /// <summary>
        /// Whether a sign-in is refused when the network it comes from is not on the
        /// account's allow lists. Off means the lists are stored but ignored.
        /// </summary>
        public bool GeoProtectionEnabled { get; init; }

        /// <summary>
        /// Whether a session is bound to the address it was opened from. A refresh that
        /// arrives from another address ends the session instead of rotating it.
        /// </summary>
        public bool BindSessionToIp { get; init; }

        /// <summary>Creation timestamp of the account.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>Last modification timestamp of the account.</summary>
        public required DateTimeOffset UpdatedAt { get; init; }
    }
}