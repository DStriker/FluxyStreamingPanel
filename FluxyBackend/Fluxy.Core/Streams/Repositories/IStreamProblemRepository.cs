using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.ReadModels;

namespace Fluxy.Core.Streams.Repositories
{
    /// <summary>
    /// Reads the stream problem journal, counts it, stages new rows, and wipes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The repository owns the query shape.</b> The predicate every filter contributes, the
    /// count taken under that same predicate, the order with its tie-break and the page cut all
    /// live in one place, so <c>total</c> can never describe a different set from the page it sits
    /// beside. The service owns the write rules - the trim, the blank refusal, the truncation -
    /// and nothing about SQL.
    /// </para>
    /// <para>
    /// <b>Writes are staged, never saved.</b> <see cref="AddAsync"/> hands the row to the tracker;
    /// the service owns the single save, so the caller that records a problem decides the
    /// transaction boundary.
    /// </para>
    /// </remarks>
    public interface IStreamProblemRepository
    {
        /// <summary>
        /// Reads one filtered page of the journal, counted before paging.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many records one page holds.</param>
        /// <param name="search">Case-insensitive substring of the message, or null.</param>
        /// <param name="streamId">Stream to keep, or null for every stream.</param>
        /// <param name="dateFrom">Earliest <c>created_at</c> to keep, inclusive, or null.</param>
        /// <param name="dateTo">Latest <c>created_at</c> to keep, inclusive, or null.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<StreamProblemPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? streamId,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            StreamProblemSortField sortBy,
            StreamProblemSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages one problem row, left for the caller's single save.
        /// </summary>
        /// <param name="problem">Values the row is written with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task AddAsync(NewStreamProblem problem, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes every row of the journal and reports how many were removed.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// <para>
        /// <b>The whole table, whatever the screen was filtered to.</b> The button says "clear the
        /// journal", and a delete that obeyed a filter the caller happened not to send would wipe
        /// a different set from the one it named.
        /// </para>
        /// <para>
        /// <b><c>ExecuteDelete</c> is immediate and outside the unit of work</b>, so there is no
        /// save for the service to call and no transaction to roll back: the statement either ran
        /// or it did not. An empty table is success with a count of zero, not a refusal - the
        /// second press of a button that already did its job must not read as an error.
        /// </para>
        /// </remarks>
        /// <returns>How many rows were deleted; zero when the table was already empty.</returns>
        Task<long> ClearAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts every row of the journal.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// The seeder's idempotence probe: a table that already holds anything is a table the
        /// producer has used, and inserting trial rows over it would mix invented evidence into a
        /// real journal.
        /// </remarks>
        Task<int> CountAsync(CancellationToken cancellationToken = default);
    }
}
