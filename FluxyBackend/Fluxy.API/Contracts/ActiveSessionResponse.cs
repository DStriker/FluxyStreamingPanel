namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What the account is shown about one active session.
    /// </summary>
    /// <remarks>
    /// The GeoIP fields arrive already resolved rather than as an address the page is expected
    /// to look up itself: the resolver is server side, its database is not shipped to the
    /// browser, and a page that resolved its own addresses would need the same file it has no
    /// way to obtain. <see cref="Id"/> is the session, not a row: naming it here is what lets
    /// the revoke endpoint end the whole chain by one value.
    /// </remarks>
    public sealed record ActiveSessionResponse
    {
        /// <summary>Identifier of the session, unique per sign-in.</summary>
        public required string Id { get; init; }

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

        /// <summary>When the session was last seen, as an ISO 8601 instant.</summary>
        public required DateTimeOffset LastSeenAt { get; init; }

        /// <summary>Whether this session is the one the list is being read with.</summary>
        public required bool IsCurrent { get; init; }
    }

    /// <summary>
    /// Every session an account currently holds, newest first.
    /// </summary>
    public sealed record ActiveSessionsResponse
    {
        /// <summary>The account's live sessions, newest first.</summary>
        public required ActiveSessionResponse[] Items { get; init; }
    }

    /// <summary>
    /// The answer to <c>DELETE /auth/active-sessions/others</c>: how many sessions it ended.
    /// </summary>
    /// <remarks>
    /// Not a <see cref="MessageResponse"/>: that record is sealed, and a count is a fact about
    /// the outcome rather than a field a refusal needs - so this carries its own copy of the
    /// two fields every answer has, plus the number.
    /// </remarks>
    public sealed record RevokeOtherSessionsResponse
    {
        /// <summary>Machine readable outcome, in snake_case like every other code here.</summary>
        public required string Code { get; init; }

        /// <summary>Human readable fallback, in English.</summary>
        public required string Message { get; init; }

        /// <summary>How many sessions were ended. Zero means the account held only the current one.</summary>
        public required int RevokedCount { get; init; }
    }
}
