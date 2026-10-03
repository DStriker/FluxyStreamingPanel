namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// What a profile change or its confirmation produced.
    /// </summary>
    /// <remarks>
    /// Carries no code and no human readable text - both belong to the transport layer, the same
    /// reason <see cref="RegistrationOutcome"/> has none either.
    /// </remarks>
    public sealed record ProfileChangeOutcome
    {
        /// <summary>How the operation ended.</summary>
        public required ProfileChangeStatus Status { get; init; }

        /// <summary>
        /// Rejected fields and the reasons they were rejected, keyed by the name of the property
        /// the service rejected. Null when nothing was rejected.
        /// </summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

        /// <summary>
        /// Whether a pending row was written. It is what the attempt throttle spends a permit
        /// on when the change needed a code, and it is independent of <see cref="Status"/>: a
        /// change that was stored but whose code could not be delivered is still stored.
        /// Always false for a confirmation, which never creates a row.
        /// </summary>
        public bool RowPersisted { get; init; }

        /// <summary>
        /// The operation the pending row was about, so the transport layer can tell a password
        /// change from a username change without reading the row again. Set whenever a change
        /// was applied or confirmed, and null otherwise.
        /// </summary>
        public PendingChangeKind? Kind { get; init; }

        /// <summary>
        /// The account as it stands after the change, set on
        /// <see cref="ProfileChangeStatus.Applied"/> and on a successful confirmation, and null
        /// for every other outcome.
        /// </summary>
        /// <remarks>
        /// It is here so the transport layer can mint a fresh access token without a second
        /// lookup: a username lives in the <c>name</c> claim, so the token in the browser still
        /// names the old one until it expires, and handing the caller the account is what lets
        /// it replace the pair at the moment of the change rather than minutes later.
        ///
        /// It carries the password hash, like <see cref="User"/> does, and like
        /// <see cref="User"/> it is a business model rather than a response contract - nothing
        /// in the API layer serializes it.
        /// </remarks>
        public User? Account { get; init; }
    }
}
