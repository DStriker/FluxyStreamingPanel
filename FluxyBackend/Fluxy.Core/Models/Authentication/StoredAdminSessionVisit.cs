namespace Fluxy.Core.Models.Authentication
{
    /// <summary>
    /// One refresh token row as seen by an operator, with the account it belongs to.
    /// </summary>
    /// <remarks>
    /// The account's name travels with the row because the page is the installation's history,
    /// not one account's: without it the operator would have a wall of addresses and no way to
    /// say whose session a row describes. Which accounts are visible at all was decided before
    /// the page was queried.
    /// </remarks>
    public sealed record StoredAdminSessionVisit
    {
        /// <summary>Identifier of the token row.</summary>
        public required Guid Id { get; init; }

        /// <summary>Session this token belongs to.</summary>
        public required Guid SessionId { get; init; }

        /// <summary>Account the token signs in.</summary>
        public required Guid UserId { get; init; }

        /// <summary>Login name of that account.</summary>
        public required string Username { get; init; }

        /// <summary>Address the sign-in or refresh came from, if one was stored.</summary>
        public string? ClientAddress { get; init; }

        /// <summary>User agent the sign-in or refresh came with, if one was stored.</summary>
        public string? UserAgent { get; init; }

        /// <summary>Moment the token was written.</summary>
        public required DateTimeOffset VisitedAt { get; init; }
    }

    /// <summary>
    /// One page of the installation's visit history, with the total the pager must show.
    /// </summary>
    public sealed record StoredAdminSessionVisitPage
    {
        /// <summary>Number of rows matching the filter, before the page was taken.</summary>
        public required int Total { get; init; }

        /// <summary>The rows of this page, in the requested order.</summary>
        public required IReadOnlyList<StoredAdminSessionVisit> Items { get; init; }
    }
}
