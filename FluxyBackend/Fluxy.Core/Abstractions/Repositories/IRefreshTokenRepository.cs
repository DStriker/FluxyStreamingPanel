using Fluxy.Core.Models.Authentication;
using Fluxy.Core.Models.Users;

namespace Fluxy.Core.Abstractions.Repositories
{
    /// <summary>
    /// Reads and writes the refresh token rows that every session is made of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Staged methods (<see cref="AddAsync"/>, <see cref="SpendAsync"/>) are committed by
    /// <see cref="IUnitOfWork"/>, so one rotation is one save. Revokes and cleanup use
    /// <c>ExecuteUpdate</c>/<c>ExecuteDelete</c> and are therefore immediate: they are not part
    /// of the unit of work, and each one is a completed act on its own.
    /// </para>
    /// <para>
    /// Every method that can answer with a time takes it as a parameter rather than reading a
    /// clock itself. The service that owns the flow has one instant for the whole rotation, and
    /// a repository that read its own clock would make the expiry checks and the writes disagree
    /// by however long the request happened to take.
    /// </para>
    /// </remarks>
    public interface IRefreshTokenRepository
    {
        /// <summary>
        /// Finds the row whose stored hash matches, or null when no row carries it.
        /// </summary>
        /// <param name="tokenHash">Stored form of the presented token.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<StoredRefreshToken?> FindByHashAsync(
            string tokenHash,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a new token row for the next save.
        /// </summary>
        /// <param name="token">Values the row is written with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task AddAsync(
            StoredRefreshToken token,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages the spending of one token: it is stamped as revoked and linked to the token
        /// issued in its place.
        /// </summary>
        /// <param name="tokenId">Row that was presented.</param>
        /// <param name="replacementId">Row issued in exchange for it.</param>
        /// <param name="now">Moment the token was spent.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SpendAsync(
            Guid tokenId,
            Guid replacementId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes every expired token and every revoked token older than the audit window.
        /// </summary>
        /// <param name="now">Moment the cleanup is measured from.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task CleanupExpiredAsync(
            DateTimeOffset now,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Ends every live row of one session.
        /// </summary>
        /// <param name="sessionId">Session to end.</param>
        /// <param name="now">Moment the rows are stamped with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Whether any live row was ended.</returns>
        Task<bool> RevokeSessionAsync(
            Guid sessionId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Ends every open row of every session of one account.
        /// </summary>
        /// <param name="userId">Account whose sessions end.</param>
        /// <param name="now">Moment the rows are stamped with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The distinct sessions whose rows were ended, read before the update. The caller
        /// uses them to withdraw the access tokens of each session as well, so a count of rows
        /// would not be enough.
        /// </returns>
        Task<IReadOnlyList<Guid>> RevokeAllSessionsAsync(
            Guid userId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Ends one session of one account, without telling an unknown session apart from one
        /// that belongs to somebody else.
        /// </summary>
        /// <param name="userId">Account the session must belong to.</param>
        /// <param name="sessionId">Session to end.</param>
        /// <param name="now">Moment the rows are stamped with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Whether a live row of that account and session was ended.</returns>
        Task<bool> RevokeUserSessionAsync(
            Guid userId,
            Guid sessionId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Ends every live session of one account except the one named.
        /// </summary>
        /// <param name="userId">Account whose sessions end.</param>
        /// <param name="exceptSessionId">Session to spare.</param>
        /// <param name="now">Moment the rows are stamped with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The sessions that were ended, read before the update. The caller uses them to
        /// withdraw the access tokens of each session as well, so counting rows would not be
        /// enough; the count of sessions is the length of this list.
        /// </returns>
        Task<IReadOnlyList<Guid>> RevokeAllExceptCurrentAsync(
            Guid userId,
            Guid exceptSessionId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the freshest live row of each session an account holds, newest first.
        /// </summary>
        /// <param name="userId">Account whose sessions are read.</param>
        /// <param name="now">Moment a row must still expire after to count as live.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<IReadOnlyList<StoredActiveSession>> GetActiveSessionsAsync(
            Guid userId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads one page of an account's visit history and the total under the same filter.
        /// </summary>
        /// <param name="userId">Account whose history is read.</param>
        /// <param name="offset">Rows to skip. A value past the total reads as an empty page.</param>
        /// <param name="pageSize">Maximum rows to return.</param>
        /// <param name="search">Case-insensitive substring of address or user agent, or null.</param>
        /// <param name="sortBy">Column the rows are ordered by.</param>
        /// <param name="sortOrder">Direction of that order.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<StoredSessionVisitPage> GetHistoryAsync(
            Guid userId,
            long offset,
            int pageSize,
            string? search,
            SessionSortField sortBy,
            SessionSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads one page of the installation's visit history, scoped to the roles the operator
        /// may see, and the total under the same filter.
        /// </summary>
        /// <param name="visibleRoles">Roles whose accounts the operator may read.</param>
        /// <param name="userId">Account whose rows only are wanted, or null for all visible.</param>
        /// <param name="offset">Rows to skip.</param>
        /// <param name="pageSize">Maximum rows to return.</param>
        /// <param name="search">Case-insensitive substring of address or user agent, or null.</param>
        /// <param name="sortBy">Column the rows are ordered by.</param>
        /// <param name="sortOrder">Direction of that order.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<StoredAdminSessionVisitPage> GetAdminHistoryAsync(
            IReadOnlySet<UserRole> visibleRoles,
            Guid? userId,
            long offset,
            int pageSize,
            string? search,
            SessionSortField sortBy,
            SessionSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Which of the named sessions still hold a row that is neither revoked nor expired.
        /// </summary>
        /// <param name="sessionIds">Sessions to ask about.</param>
        /// <param name="now">Moment a row must still expire after to count as live.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<IReadOnlySet<Guid>> GetLiveSessionIdsAsync(
            IReadOnlyCollection<Guid> sessionIds,
            DateTimeOffset now,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds the account a session belongs to, or null when no row carries the session.
        /// </summary>
        /// <param name="sessionId">Session to find.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<Guid?> GetSessionOwnerAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default);
    }
}
