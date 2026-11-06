using Fluxy.Core.Users.Commands;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.ReadModels;

namespace Fluxy.Core.Users.Repositories
{
    /// <summary>
    /// Reads the reseller credit log, stages new entries, and wipes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The repository owns the query shape.</b> The predicate every filter contributes, the
    /// count taken under that same predicate, the order with its tie-break and the two joins that
    /// resolve the usernames all live in one place, so <c>total</c> can never describe a different
    /// set from the page it sits beside.
    /// </para>
    /// <para>
    /// <b>Writes are staged, never saved.</b> A credit log row is only ever written together with
    /// the balance change it explains: the service stages both and commits them with one save, so
    /// a failure leaves neither behind. There is no intent method that saves, and none that writes
    /// a log row on its own.
    /// </para>
    /// </remarks>
    public interface ICreditLogRepository
    {
        /// <summary>
        /// Reads one filtered page of the log, counted before paging.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many entries one page holds.</param>
        /// <param name="search">
        /// Case-insensitive substring of the description or the reseller's username, or null.
        /// </param>
        /// <param name="resellerId">Reseller to keep, or null for every reseller.</param>
        /// <param name="dateFrom">Earliest <c>created_at</c> to keep, inclusive, or null.</param>
        /// <param name="dateTo">Latest <c>created_at</c> to keep, inclusive, or null.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<CreditLogPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? resellerId,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            CreditLogSortField sortBy,
            CreditLogSortOrder sortOrder,
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
        /// Stages one new log row, left for the caller's single save.
        /// </summary>
        /// <param name="entry">Values the row is written with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// Staged rather than saved on purpose: the balance change and the row that explains it
        /// are one operation, and the service commits both with one <c>TrySaveAsync</c>. A save
        /// here would make a credit change and its audit trail two operations that could diverge.
        /// </remarks>
        Task AddAsync(NewCreditLog entry, CancellationToken cancellationToken = default);
    }
}
