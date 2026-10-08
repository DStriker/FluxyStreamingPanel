namespace Fluxy.API.Contracts
{
    /// <summary>
    /// The two switches and the three allow lists, as one object rather than as five fields
    /// beside the account.
    /// </summary>
    /// <remarks>
    /// Nested here rather than flattened like <see cref="ProfileResponse"/>: that one spells a
    /// guard out across the body because the body *is* the guard plus nothing, while this body
    /// is an account and the guard is one section of its edit form. A form that reads
    /// <c>user.loginGuard.allowedIps</c> cannot confuse it with a field of the account itself.
    /// </remarks>
    public sealed record LoginGuardResponse
    {
        /// <summary>
        /// Whether a sign-in from a network that is not allowed is refused with the same
        /// answer a wrong password gets.
        /// </summary>
        public required bool GeoProtectionEnabled { get; init; }

        /// <summary>Whether a refresh from an address other than the session's own ends it.</summary>
        public required bool BindSessionToIp { get; init; }

        /// <summary>Allowed addresses, exact or CIDR, IPv4 or IPv6.</summary>
        public required string[] AllowedIps { get; init; }

        /// <summary>Allowed country, ISO 3166-1 alpha-2, or null when not restricted.</summary>
        public string? AllowedCountry { get; init; }

        /// <summary>Allowed provider by autonomous system number, or null when not restricted.</summary>
        public int? AllowedAutonomousSystemNumber { get; init; }
    }

    /// <summary>
    /// One account in full, for the edit form.
    /// </summary>
    /// <remarks>
    /// The identifier is a string rather than a value the form could submit: it is shown as
    /// static text and travels back in the <c>PATCH</c> body's path, never as a field of the
    /// body - a form that posted its own id as data would be one edit away from writing it.
    ///
    /// There is no password here and no pending code here. The first cannot be shown back and
    /// the second is a credential, so both are absent rather than empty.
    /// </remarks>
    public sealed record AdminUserDetailResponse
    {
        /// <summary>Identifier of the account.</summary>
        public required string Id { get; init; }

        /// <summary>Login name, at most 20 characters. Unique, compared case sensitively.</summary>
        public required string Username { get; init; }

        /// <summary>Email address of the owner, stored normalized.</summary>
        public required string Email { get; init; }

        /// <summary>Access level, as the name of the enum member.</summary>
        public required string Role { get; init; }

        /// <summary>State of the account, as the name of the enum member.</summary>
        public required string Status { get; init; }

        /// <summary>
        /// IANA identifier of the chosen display time zone, or null when the account has not
        /// chosen one and its browser decides.
        /// </summary>
        public string? TimeZone { get; init; }

        /// <summary>
        /// Moment the account confirmed its email address, or null while it has never been
        /// registered. Blocking does not clear it.
        /// </summary>
        public DateTimeOffset? RegisteredAt { get; init; }

        /// <summary>When the row was created.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>When the account last signed in or refreshed, or null when it never has.</summary>
        public DateTimeOffset? LastSeenAt { get; init; }

        /// <summary>Address that visit came from, or null when the row recorded none.</summary>
        public string? LastIp { get; init; }

        /// <summary>The two switches and the three allow lists of this account.</summary>
        public required LoginGuardResponse LoginGuard { get; init; }
    }
}
