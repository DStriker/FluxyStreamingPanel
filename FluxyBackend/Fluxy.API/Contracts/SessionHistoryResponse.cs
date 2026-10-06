namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What the account is shown about one entry of its visit history.
    /// </summary>
    /// <remarks>
    /// The GeoIP fields arrive already resolved rather than as an address the page is expected
    /// to look up itself: the resolver is server side, its database is not shipped to the
    /// browser, and a page that resolved its own addresses would need the same file it has no
    /// way to obtain.
    /// </remarks>
    public sealed record SessionVisitResponse
    {
        /// <summary>Identifier of the history row, unique per token.</summary>
        public required string Id { get; init; }

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

        /// <summary>Whether this entry belongs to the session the list is being read with.</summary>
        public required bool IsCurrent { get; init; }
    }

    /// <summary>
    /// One page of an account's visit history.
    /// </summary>
    public sealed record SessionHistoryResponse
    {
        /// <summary>Visits on this page, newest first.</summary>
        public required SessionVisitResponse[] Items { get; init; }

        /// <summary>How many visits the account has in total, for the pager.</summary>
        public required int Total { get; init; }

        /// <summary>The page these visits are, one based.</summary>
        public required int Page { get; init; }

        /// <summary>How many visits one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
