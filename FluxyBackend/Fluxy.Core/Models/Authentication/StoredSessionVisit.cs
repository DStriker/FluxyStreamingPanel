using Fluxy.Core.Users.Models;

namespace Fluxy.Core.Models.Authentication
{
    /// <summary>
    /// One refresh token row as a visit of the account's own history.
    /// </summary>
    /// <remarks>
    /// The history lists tokens rather than sessions on purpose: a rotation is a visit, and a
    /// browser that changed networks between two refreshes must show up twice. The identifier
    /// of the session is carried beside the identifier of the row because the page marks the
    /// session the caller is reading from, not the row.
    /// </remarks>
    public sealed record StoredSessionVisit
    {
        /// <summary>Identifier of the token row.</summary>
        public required Guid Id { get; init; }

        /// <summary>Session this token belongs to.</summary>
        public required Guid SessionId { get; init; }

        /// <summary>Address the sign-in or refresh came from, if one was stored.</summary>
        public string? ClientAddress { get; init; }

        /// <summary>User agent the sign-in or refresh came with, if one was stored.</summary>
        public string? UserAgent { get; init; }

        /// <summary>Moment the token was written.</summary>
        public required DateTimeOffset VisitedAt { get; init; }
    }

    /// <summary>
    /// One page of an account's visit history, with the total the pager must show.
    /// </summary>
    /// <remarks>
    /// The total is counted under the same filter as the page. A count that knew nothing about
    /// the search would advertise pages the caller was never going to be shown.
    /// </remarks>
    public sealed record StoredSessionVisitPage
    {
        /// <summary>Number of rows matching the filter, before the page was taken.</summary>
        public required int Total { get; init; }

        /// <summary>The rows of this page, in the requested order.</summary>
        public required IReadOnlyList<StoredSessionVisit> Items { get; init; }
    }
}
