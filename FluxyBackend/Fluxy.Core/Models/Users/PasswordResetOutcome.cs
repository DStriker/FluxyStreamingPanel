namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// What a password reset request or its confirmation produced.
    /// </summary>
    public sealed record PasswordResetOutcome
    {
        /// <summary>How the operation ended.</summary>
        public required PasswordResetStatus Status { get; init; }

        /// <summary>
        /// Rejected fields and the reasons they were rejected, keyed by the name of the property
        /// the service rejected. Null when nothing was rejected.
        /// </summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

        /// <summary>
        /// Whether a pending row was written. It is what the attempt throttle spends a permit
        /// on, and it is independent of <see cref="Status"/>: a reset that was stored but whose
        /// code could not be delivered is still stored.
        /// </summary>
        public bool RowPersisted { get; init; }

        /// <summary>
        /// The account whose password was just replaced, set on
        /// <see cref="PasswordResetStatus.Confirmed"/> and null for every other outcome. The
        /// transport layer needs it to end the sessions of that account.
        /// </summary>
        public Guid? UserId { get; init; }
    }
}
