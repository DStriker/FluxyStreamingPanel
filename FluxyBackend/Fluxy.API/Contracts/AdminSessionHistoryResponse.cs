namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What an operator is shown about one entry of the installation's visit history.
    /// </summary>
    /// <remarks>
    /// <see cref="SessionVisitResponse"/> plus the account this visit belongs to and the two
    /// things the button beside the row needs: the session to end, and whether it can be ended
    /// at all. The GeoIP fields arrive already resolved rather than as an address the page is
    /// expected to look up itself - the resolver is server side, its database is not shipped to
    /// the browser, and a page that resolved its own addresses would need the same file it has
    /// no way to obtain.
    /// </remarks>
    public sealed record AdminSessionVisitResponse
    {
        /// <summary>Identifier of the history row, unique per token.</summary>
        public required string Id { get; init; }

        /// <summary>
        /// Session this visit belongs to, which is what ending it names. Several rows of one
        /// session carry the same value: each rotation is a visit, and all of them are one
        /// sign-in.
        /// </summary>
        public required string SessionId { get; init; }

        /// <summary>Identifier of the account the session belongs to.</summary>
        public required string UserId { get; init; }

        /// <summary>Username of that account, so the row does not have to be opened to read it.</summary>
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

        /// <summary>When the token was issued or rotated, as an ISO 8601 instant.</summary>
        public required DateTimeOffset VisitedAt { get; init; }

        /// <summary>
        /// Whether this entry belongs to the session the list is being read with, so the
        /// operator's own browser is not mistaken for an intruder's.
        /// </summary>
        public required bool IsCurrent { get; init; }

        /// <summary>
        /// Whether the session behind this row is still signed in and its button may do
        /// something. False for a chain that was ended or quietly expired.
        /// </summary>
        public required bool IsActive { get; init; }
    }

    /// <summary>
    /// One page of the installation's visit history.
    /// </summary>
    public sealed record AdminSessionHistoryResponse
    {
        /// <summary>Visits on this page, newest first.</summary>
        public required AdminSessionVisitResponse[] Items { get; init; }

        /// <summary>How many visits the operator may see in total, for the pager.</summary>
        public required int Total { get; init; }

        /// <summary>The page these visits are, one based.</summary>
        public required int Page { get; init; }

        /// <summary>How many visits one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
