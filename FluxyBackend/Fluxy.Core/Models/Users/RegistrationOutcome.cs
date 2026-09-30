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
    }
}
