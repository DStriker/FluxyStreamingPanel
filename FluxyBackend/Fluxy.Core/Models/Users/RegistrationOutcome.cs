namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// What a registration or confirmation call produced.
    /// </summary>
    /// <remarks>
    /// The outcome carries a status and, for rejected input, the offending fields. It carries
    /// no code and no human readable text: both belong to the transport layer, and keeping them
    /// here is what lets the same outcome be rendered into different codes and languages without
    /// the service knowing about either.
    /// </remarks>
    public sealed record RegistrationOutcome
    {
        /// <summary>How the operation ended.</summary>
        public required RegistrationStatus Status { get; init; }

        /// <summary>
        /// Rejected fields and the reasons they were rejected, keyed by the name of the
        /// property of <see cref="NewRegistration"/>. Null when nothing was rejected.
        /// </summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

        /// <summary>
        /// Whether a user row was actually written. It is what the attempt throttle consumes a
        /// permit on, and it is deliberately independent of <see cref="Status"/>: an account
        /// that was stored but whose code could not be delivered is still stored, and has to be
        /// paid for. Always false for confirmations, which never create a row.
        /// </summary>
        public bool RowPersisted { get; init; }

        /// <summary>
        /// The account a confirmation has just activated, or null for every other outcome.
        /// </summary>
        /// <remarks>
        /// It is here so the transport layer can sign the visitor in without a second lookup and,
        /// more importantly, without having to find the account by the address the visitor
        /// happens to have typed. The caller supplied that address as proof of ownership of a
        /// mailbox; it is not an identifier, and treating it as one would put the ability to open
        /// a session behind a string anybody who had once known the address could produce.
        ///
        /// The account comes from the row that was verified - the one whose code matched - and
        /// from nowhere else. It is set on <see cref="RegistrationStatus.Confirmed"/> and only
        /// there, so a caller cannot receive an account it did not prove.
        /// </remarks>
        public User? ConfirmedAccount { get; init; }
    }
}
