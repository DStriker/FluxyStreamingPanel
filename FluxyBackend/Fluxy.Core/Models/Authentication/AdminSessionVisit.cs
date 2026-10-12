namespace Fluxy.Core.Models.Authentication
{
    /// <summary>
    /// One entry of the installation's visit history, with the account whose sessions wrote it.
    /// </summary>
    /// <remarks>
    /// <see cref="SessionVisit"/> plus two things only somebody reading another account's rows
    /// needs: <see cref="UserId"/> and <see cref="Username"/> say who this was, and
    /// <see cref="SessionId"/> is what the "end session" button acts on - a row is a token, but
    /// the session a token belongs to is the chain the whole thing hangs from, and ending it by
    /// the token's own id would only ever end one link.
    ///
    /// <see cref="IsActive"/> carries the same rule the active sessions list is built on: a
    /// session is live while one row of its chain is neither revoked nor expired. It is answered
    /// here rather than left for the page to guess, because the freshness of this particular row
    /// says nothing about the chain - an older spent rotation is still a legitimate visit while
    /// its session is very much alive, and a lone expired row is a session that can no longer be
    /// ended at all.
    ///
    /// Nothing here is a credential: the address, the user agent and the moment are recorded for
    /// the session's own record, and the token itself is kept only as a hash elsewhere.
    /// </remarks>
    public sealed record AdminSessionVisit
    {
        /// <summary>
        /// Identifier of the history row. Unique per token, so a session that rotated several
        /// times contributes several distinguishable entries rather than repeated keys.
        /// </summary>
        public required Guid Id { get; init; }

        /// <summary>
        /// Session this token belongs to, which is what ending a session from this list names.
        /// </summary>
        public required Guid SessionId { get; init; }

        /// <summary>Account the sessions behind this row belong to.</summary>
        public required Guid UserId { get; init; }

        /// <summary>Its username, so the operator does not have to open the row to read it.</summary>
        public required string Username { get; init; }

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
        /// Whether this entry belongs to the session the list is being read with. An operator
        /// scanning for a device they do not recognise would otherwise take the newest row of
        /// their own browser for an intruder.
        /// </summary>
        public required bool IsCurrent { get; init; }

        /// <summary>
        /// Whether the session this row belongs to can still be ended - one of its tokens is
        /// neither revoked nor expired. False means the chain is dead and pressing the button
        /// would change nothing, which is why the refusal the endpoint gives for it is the same
        /// 404 as a session nobody has ever heard of.
        /// </summary>
        public required bool IsActive { get; init; }
    }
}
