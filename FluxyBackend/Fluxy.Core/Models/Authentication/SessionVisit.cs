namespace Fluxy.Core.Models.Authentication
{
    /// <summary>
    /// One entry of an account's visit history: a refresh token as the network it arrived from.
    /// </summary>
    /// <remarks>
    /// A row of the history is a token and not a session, deliberately. Every sign-in mints one
    /// and every rotation mints another, so a browser that moved from the office to home between
    /// two refreshes shows up twice - which is the whole point of the page. Aggregating to one
    /// row per session would hide exactly the fact a person reading this list is looking for.
    ///
    /// Nothing here is a credential: the address, the user agent and the moment are recorded for
    /// the session's own record, and the token itself is kept only as a hash elsewhere.
    /// </remarks>
    public sealed record SessionVisit
    {
        /// <summary>
        /// Identifier of the history row. Unique per token, so a session that rotated several
        /// times contributes several distinguishable entries rather than repeated keys.
        /// </summary>
        public required Guid Id { get; init; }

        /// <summary>Address the request arrived from, or null when the connection had none.</summary>
        public string? Ip { get; init; }

        /// <summary>Country of the address, ISO 3166-1 alpha-2 upper case, or null when unknown.</summary>
        public string? CountryCode { get; init; }

        /// <summary>Autonomous system number of the address, or null when unknown.</summary>
        public int? AutonomousSystemNumber { get; init; }

        /// <summary>Organization the autonomous system is registered to, or null when unknown.</summary>
        public string? Organization { get; init; }

        /// <summary>User agent the request carried, or null when it carried none.</summary>
        public string? UserAgent { get; init; }

        /// <summary>When the token was issued or rotated - the moment of the visit itself.</summary>
        public required DateTimeOffset VisitedAt { get; init; }

        /// <summary>
        /// Whether this entry belongs to the session the caller is reading it with. A person
        /// scanning the list for an address they do not recognise needs their own current
        /// device marked, otherwise the newest row is only "probably me".
        /// </summary>
        public required bool IsCurrent { get; init; }
    }
}
