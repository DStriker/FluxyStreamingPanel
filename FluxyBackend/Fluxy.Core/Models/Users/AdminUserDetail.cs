namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// One account in full: everything the edit form draws, and still nothing that is a secret.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="AdminUser"/> for the same reason the model is separate from its
    /// list row - the edit page needs the time zone and the login guard, the table needs
    /// neither, and one shape serving both would make the table's columns a property of the
    /// form's fields.
    ///
    /// There is no password here and no pending code here: the first cannot be shown back and
    /// the second is a credential, so both are absent rather than empty.
    /// </remarks>
    public sealed record AdminUserDetail
    {
        /// <summary>Identifier of the account. Shown as static text, never as a payload field.</summary>
        public required Guid Id { get; init; }

        /// <summary>Login name, at most 20 characters. Unique, compared case sensitively.</summary>
        public required string Username { get; init; }

        /// <summary>Email address of the owner, stored normalized.</summary>
        public required string Email { get; init; }

        /// <summary>Access level of the account, inherited from its group.</summary>
        public required UserRole Role { get; init; }

        /// <summary>Identifier of the group the account belongs to, which the form picks.</summary>
        public required Guid GroupId { get; init; }

        /// <summary>Display name of that group.</summary>
        public required string GroupName { get; init; }

        /// <summary>
        /// The state stored on the account's own row, which is the value the form edits.
        /// </summary>
        /// <remarks>
        /// Deliberately the row's own state rather than the effective one that
        /// <see cref="AdminUser"/> shows in the list. The two differ whenever the group is
        /// what blocks the account, and handing the form the effective value would have the
        /// operator's untouched dropdown write <c>Blocked</c> onto a row that was never
        /// blocked - an accidental change, silently applied, the moment anything else on the
        /// page was saved. The effective one travels beside it as
        /// <see cref="EffectiveStatus"/> so the form can state the difference instead of
        /// appearing to contradict the row the operator just came from.
        /// </remarks>
        public required UserStatus Status { get; init; }

        /// <summary>
        /// What the account actually is right now: <see cref="Status"/> combined with its
        /// group's state. Display only - no request writes it.
        /// </summary>
        public required UserStatus EffectiveStatus { get; init; }

        /// <summary>
        /// IANA identifier of the chosen display time zone, or null when the account has not
        /// chosen one and its browser decides.
        /// </summary>
        public string? TimeZone { get; init; }

        /// <summary>
        /// Moment the account confirmed its email address, or null while it has never been
        /// registered. Blocking does not clear it - see <see cref="User.RegisteredAt"/>.
        /// </summary>
        public DateTimeOffset? RegisteredAt { get; init; }

        /// <summary>When the row was created.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>When the account last signed in or refreshed, or null when it never has.</summary>
        public DateTimeOffset? LastSeenAt { get; init; }

        /// <summary>Address that visit came from, or null when the row recorded none.</summary>
        public string? LastIp { get; init; }

        /// <summary>The two switches and the three allow lists of this account.</summary>
        public required LoginGuardSettings LoginGuard { get; init; }
    }
}
