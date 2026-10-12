using Fluxy.Core.Models.Authentication;
using Fluxy.Core.Models.Users;

namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Reads the visit history of the accounts one operator may see, and ends their sessions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The admin counterpart of <see cref="ISessionHistoryService"/>, and the reason it is its
    /// own interface rather than an extra method on that one is the scoping. The self-service
    /// reader takes the account from the access token and has exactly one row set to consider;
    /// this one takes <c>granted</c> from the request and has to decide which of the
    /// installation's accounts are in scope at all - a question <see cref="ISessionHistoryService"/>
    /// has no shape for. Merging the two would mean every read of one's own history carried a
    /// permission argument it could not use.
    /// </para>
    /// <para>
    /// It is the one service of this pair that writes, and only in one direction: it ends
    /// sessions, it never touches a history row. Rows appear when a token is issued and
    /// disappear when the existing cleanup retires them, and ending a session stamps
    /// <c>revoked_at</c> rather than deleting anything - the record of when a chain was cut is
    /// exactly what somebody auditing this page came to read.
    /// </para>
    /// <para>
    /// The interface says nothing about HTTP, pages of JSON or status codes. It reports a page,
    /// or one of three outcomes, and which of those becomes a 400, 404 or 403 is the transport's
    /// business.
    /// </para>
    /// </remarks>
    public interface IAdminSessionHistoryService
    {
        /// <summary>
        /// Reads one page of the visits the caller may see, newest first.
        /// </summary>
        /// <param name="page">One based page to read. Values below one are read as the first.</param>
        /// <param name="pageSize">
        /// How many visits the page holds, clamped to
        /// <see cref="SessionHistoryLimits.MaxPageSize"/>. The endpoint refuses an out of range
        /// value before this service sees it; the clamp is here so that a caller inside the
        /// application cannot ask for an unbounded read either.
        /// </param>
        /// <param name="search">
        /// Case-insensitive text that must appear in the address or the user agent of a visit.
        /// Null or blank means every visit counts.
        /// </param>
        /// <param name="userId">
        /// Account to restrict the list to, or null for every account the caller may read. The
        /// caller's own permission set is what decides whether this narrows anything: naming an
        /// account of a role the caller cannot read simply matches no rows, rather than being
        /// refused as a permission problem the caller could not have known about in advance.
        /// </param>
        /// <param name="sortBy">What the page is ordered by.</param>
        /// <param name="sortOrder">Which way it runs.</param>
        /// <param name="granted">Permissions the caller's group holds.</param>
        /// <param name="currentSessionId">
        /// Session the caller is reading with, used to mark its own entries. Null when the token
        /// carried no session claim, which marks nothing rather than guessing.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The page, with <see cref="AdminSessionHistoryPage.Total"/> counting every visit the
        /// caller may see rather than only the rows of this page. A caller holding no view
        /// permission at all is answered an empty page - the coarse policy ahead of this refuses
        /// such a caller before it gets here, so the empty page is a floor rather than a branch
        /// anybody should reach.
        /// </returns>
        Task<AdminSessionHistoryPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? userId,
            SessionSortField sortBy,
            SessionSortOrder sortOrder,
            IReadOnlySet<UserPermission> granted,
            Guid? currentSessionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Ends one session, so that no refresh token in its chain can be exchanged again.
        /// </summary>
        /// <param name="sessionId">Session to end, as listed on the history page.</param>
        /// <param name="granted">Permissions the caller's group holds.</param>
        /// <param name="currentSessionId">
        /// Session the request is authenticated with, so ending it can be refused as a fact about
        /// the request rather than found out by a row lookup.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// One of the three <see cref="AdminSessionRevokeOutcome"/> members. Ownership is not a
        /// separate question here: the session's owner's role is checked against the same
        /// <c>granted</c> the read above is scoped by, so a session of an account this operator
        /// cannot see answers <see cref="AdminSessionRevokeOutcome.NotFound"/> exactly like one
        /// that never existed.
        /// </returns>
        Task<AdminSessionRevokeOutcome> RevokeAsync(
            Guid sessionId,
            IReadOnlySet<UserPermission> granted,
            Guid? currentSessionId,
            CancellationToken cancellationToken = default);
    }
}
