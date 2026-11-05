using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.ReadModels;

namespace Fluxy.Core.Subscriptions.Repositories
{
    /// <summary>
    /// Reads the streaming connection log and wipes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Read, count, insert for the trial seeder, and clear.</b> Nothing in this pass captures
    /// a connection: the process that relays the stream will own the write path, and it is not
    /// this endpoint. The one insert intent here exists for the Development trial seeder, whose
    /// rows must point at real subscriptions and streams - the foreign key is there to prevent a
    /// fabricated identifier, so the seeder cannot plant rows through raw SQL the way a test
    /// fixture can. It is storage plumbing, not capture logic: it applies no rule about which
    /// allowance was spent or which moment a row opens, and no production endpoint reaches it.
    /// </para>
    /// <para>
    /// The repository owns the query shape: the predicate every filter contributes, the count
    /// taken under that same predicate, the order with its tie-break, and the extra read that
    /// resolves the stream titles of one page. The service owns nothing but the delegation today;
    /// the transport owns which parameter becomes which answer.
    /// </para>
    /// </remarks>
    public interface IConnectionLogRepository
    {
        /// <summary>
        /// Reads one filtered page of the log, counted before paging.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many connections one page holds.</param>
        /// <param name="search">Case-insensitive substring of the address, user agent or provider, or null.</param>
        /// <param name="streamKind">Medium to keep - the caller always names exactly one.</param>
        /// <param name="liveOnly">True to keep only the sessions that have not ended.</param>
        /// <param name="subscriptionId">Subscription to keep, or null for every subscription.</param>
        /// <param name="ownerId">Owner of the subscription to keep, or null for every owner.</param>
        /// <param name="streamId">Opaque stream identifier to keep, or null for every stream.</param>
        /// <param name="ip">Address to keep, compared exactly, or null.</param>
        /// <param name="countryCode">Upper-case ISO alpha-2 country to keep, or null.</param>
        /// <param name="isp">Provider to keep, compared exactly, or null.</param>
        /// <param name="userAgent">User agent to keep, compared exactly, or null.</param>
        /// <param name="startedFrom">Earliest <c>started_at</c> to keep, inclusive, or null.</param>
        /// <param name="startedTo">Latest <c>started_at</c> to keep, inclusive, or null.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<ConnectionLogPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            ConnectionStreamKind streamKind,
            bool liveOnly,
            Guid? subscriptionId,
            Guid? ownerId,
            Guid? streamId,
            string? ip,
            string? countryCode,
            string? isp,
            string? userAgent,
            DateTimeOffset? startedFrom,
            DateTimeOffset? startedTo,
            ConnectionLogSortField sortBy,
            ConnectionLogSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes every row of the log and reports how many were removed.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// <para>
        /// <b>The whole table, whatever the screen was filtered to.</b> The button says "clear all
        /// logs", and a delete that obeyed a filter the caller happened not to send would wipe a
        /// different set from the one it named. Both media, live and finished alike.
        /// </para>
        /// <para>
        /// <b><c>ExecuteDelete</c> is immediate and outside the unit of work</b>, so there is no
        /// save for the service to call and no transaction to roll back: the statement either ran
        /// or it did not. That is the same trade the token stores make, and it is acceptable here
        /// because the operation is a single statement whose only outcome is "the table is empty".
        /// An empty table is success with a count of zero, not a refusal - the second press of a
        /// button that already did its job must not read as an error.
        /// </para>
        /// </remarks>
        /// <returns>How many rows were deleted; zero when the table was already empty.</returns>
        Task<int> ClearAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages new connection rows, left for the caller's single save.
        /// </summary>
        /// <param name="logs">Rows to stage, in the order they are given.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// <para>
        /// <b>Its only caller is the Development trial seeder.</b> It exists because the rows a
        /// screen is demonstrated with must name real subscriptions and streams - the foreign key
        /// on <c>subscription_id</c> refuses a fabricated identifier, and the title the page draws
        /// is resolved from a real stream row - and a seeder in the application layer may not
        /// reach an internal persistence type to write one.
        /// </para>
        /// <para>
        /// <b>It is storage plumbing, not capture.</b> No rule lives here about which allowance
        /// was spent, which session opened the connection or which moment it ended; those belong
        /// to the relaying process, whose write path arrives with it. There is no production
        /// endpoint behind this method, and a request cannot reach it.
        /// </para>
        /// </remarks>
        /// <returns>The number of rows staged. The caller commits them.</returns>
        Task<int> AddRangeAsync(
            IReadOnlyList<NewConnectionLog> logs,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts every row of the log.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// The seeder's idempotence probe: a table that already holds anything is a table the
        /// operator has used, and inserting trial rows over it would mix invented evidence into
        /// a real log.
        /// </remarks>
        Task<int> CountAsync(CancellationToken cancellationToken = default);
    }
}
