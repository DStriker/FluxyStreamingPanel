namespace Fluxy.Core.Models.Authentication
{
    /// <summary>
    /// One active sign-in session of an account, described by the freshest live refresh
    /// token in its chain.
    /// </summary>
    /// <remarks>
    /// A row here is a session and not a token, deliberately - the opposite of
    /// <see cref="SessionVisit"/>, and for the opposite question. The history page lists every
    /// rotation so a network change is visible; this page lists what is still signed in so a
    /// person can end what they do not recognise. One session, one row, and the fields are the
    /// ones the freshest live token carries: the address and the client it arrived with, as of
    /// its last rotation.
    ///
    /// Nothing here is a credential: the address, the user agent and the moment are recorded
    /// for the session's own record, and the token itself is kept only as a hash elsewhere.
    /// </remarks>
    public sealed record ActiveSession
    {
        /// <summary>
        /// Identifier of the session. Unique per sign-in, so a session that rotated several
        /// times still contributes exactly one row - which is what lets a caller revoke the
        /// whole chain by naming one id.
        /// </summary>
        public required Guid Id { get; init; }

        /// <summary>Address the session was last seen from, or null when the connection had none.</summary>
        public string? Ip { get; init; }

        /// <summary>Country of the address, ISO 3166-1 alpha-2 upper case, or null when unknown.</summary>
        public string? CountryCode { get; init; }

        /// <summary>Autonomous system number of the address, or null when unknown.</summary>
        public int? AutonomousSystemNumber { get; init; }

        /// <summary>Organization the autonomous system is registered to, or null when unknown.</summary>
        public string? Organization { get; init; }

        /// <summary>User agent of the freshest live token, or null when it carried none.</summary>
        public string? UserAgent { get; init; }

        /// <summary>
        /// When the freshest live token was issued or rotated - the moment the session was
        /// last seen, not the moment the account signed in.
        /// </summary>
        public required DateTimeOffset LastSeenAt { get; init; }

        /// <summary>
        /// Whether this session is the one the list is being read with. A person scanning the
        /// list for a session they do not recognise needs their own marked, otherwise the
        /// newest row is only "probably me" - and the newest row is exactly the one whose
        /// revoke button must not be pressed.
        /// </summary>
        public required bool IsCurrent { get; init; }
    }
}
