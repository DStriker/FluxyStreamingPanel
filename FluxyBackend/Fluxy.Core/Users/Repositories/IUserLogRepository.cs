using Fluxy.Core.Users.Commands;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.ReadModels;

namespace Fluxy.Core.Users.Repositories
{
    /// <summary>
    /// Reads the user log, stages new entries, and wipes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The repository owns the query shape.</b> The predicate every filter contributes, the
    /// count taken under that same predicate, the order with its tie-break and the join that
    /// resolves the login name all live in one place, so <c>total</c> can never describe a
    /// different set from the page it sits beside.
    /// </para>
    /// <para>
    /// <b>Writes are staged, never saved.</b> A log row is written by the service with its own
    /// save, after the flow it describes has already finished, so there is no intent method that
    /// commits: the caller owns the transaction boundary and this repository only hands the row to
    /// the tracker.
    /// </para>
    /// </remarks>
    public interface IUserLogRepository
    {
        /// <summary>
        /// Reads one filtered page of the log, counted before paging.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many entries one page holds.</param>
        /// <param name="search">
        /// Case-insensitive substring of the description or the account's login name, or null.
        /// </param>
        /// <param name="userIds">Accounts to keep, or null for every account.</param>
        /// <param name="groupIds">Groups to keep, or null for every group.</param>
        /// <param name="recordTypes">Record types to keep, or null for every type.</param>
        /// <param name="ip">Address to keep, compared exactly, or null.</param>
        /// <param name="countryCode">Upper-case ISO alpha-2 country to keep, or null.</param>
        /// <param name="isp">Provider to keep, compared exactly, or null.</param>
        /// <param name="dateFrom">Earliest <c>created_at</c> to keep, inclusive, or null.</param>
        /// <param name="dateTo">Latest <c>created_at</c> to keep, inclusive, or null.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<UserLogPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            IReadOnlyList<Guid>? userIds,
            IReadOnlyList<Guid>? groupIds,
            IReadOnlyList<UserLogRecordType>? recordTypes,
            string? ip,
            string? countryCode,
            string? isp,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            UserLogSortField sortBy,
            UserLogSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes every row of the log and reports how many were removed.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// <para>
        /// <b>The whole table, whatever the screen was filtered to.</b> The button says "clear all
        /// logs", and a delete that obeyed a filter the caller happened not to send would wipe a
        /// different set from the one it named.
        /// </para>
        /// <para>
        /// <b><c>ExecuteDelete</c> is immediate and outside the unit of work</b>, so there is no
        /// save for the service to call and no transaction to roll back: the statement either ran
        /// or it did not. That is acceptable here because the operation is a single statement whose
        /// only outcome is "the table is empty". An empty table is success with a count of zero,
        /// not a refusal - the second press of a button that already did its job must not read as
        /// an error.
        /// </para>
        /// </remarks>
        /// <returns>How many rows were deleted; zero when the table was already empty.</returns>
        Task<int> ClearAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages one new log row, left for the caller's save.
        /// </summary>
        /// <param name="entry">
        /// Values the row is written with. Network columns arrive already truncated and the
        /// GeoIP snapshot already resolved.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// Staged rather than saved on purpose: the service that records an event owns the one save
        /// that commits it, which is what keeps the log write out of the caller's transaction while
        /// still being a single round trip.
        /// </remarks>
        Task AddAsync(NewUserLog entry, CancellationToken cancellationToken = default);

        /// <summary>
        /// Drops the row <see cref="AddAsync"/> staged, so a later save of the same request does
        /// not retry it.
        /// </summary>
        /// <remarks>
        /// <b>The audit write fails open only when this is called.</b> A save that fails leaves the
        /// staged row in the change tracker, and the next save of the same request - the profile
        /// flow mints its replacement tokens after the change lands - would try to insert it again
        /// and fail a second time, this time with nothing left to swallow it. The service calls
        /// this from its catch, so a broken audit table costs the entry and nothing else.
        /// </remarks>
        void DiscardStaged();
    }
}
