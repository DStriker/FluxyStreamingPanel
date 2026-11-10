using Fluxy.Core.Requests.Commands;
using Fluxy.Core.Requests.Enums;
using Fluxy.Core.Requests.ReadModels;

namespace Fluxy.Core.Requests.Repositories
{
    /// <summary>
    /// Reads the API request failure log, counts it, stages new rows for the trial seeder, and
    /// wipes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Read, count, insert for the seeder, and clear.</b> Nothing in this pass records a refusal:
    /// the future public API will own that write path, and it is not this endpoint. The one insert
    /// intent here exists for the Development trial seeder, whose rows have no foreign keys to
    /// satisfy and which may not reach an internal persistence type from the application layer. It
    /// is storage plumbing, not capture logic: no rule lives here about why a request was refused,
    /// and no production endpoint reaches it.
    /// </para>
    /// <para>
    /// The repository owns the query shape: the predicate every filter contributes, the count
    /// taken under that same predicate, and the order with its tie-break. The service owns nothing
    /// but the delegation today; the transport owns which parameter becomes which answer.
    /// </para>
    /// </remarks>
    public interface IApiRequestFailureRepository
    {
        /// <summary>
        /// Reads one filtered page of the log, counted before paging.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many records one page holds.</param>
        /// <param name="search">
        /// Case-insensitive substring of the description, the subscription data or the content
        /// name, or null.
        /// </param>
        /// <param name="reasons">Reasons to keep, or null for every reason.</param>
        /// <param name="ip">Address to keep, compared exactly, or null.</param>
        /// <param name="countryCode">Upper-case ISO alpha-2 country to keep, or null.</param>
        /// <param name="isp">Provider to keep, compared exactly, or null.</param>
        /// <param name="dateFrom">Earliest <c>created_at</c> to keep, inclusive, or null.</param>
        /// <param name="dateTo">Latest <c>created_at</c> to keep, inclusive, or null.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<ApiRequestFailurePage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            IReadOnlyList<ApiRequestFailureReason>? reasons,
            string? ip,
            string? countryCode,
            string? isp,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            ApiRequestFailureSortField sortBy,
            ApiRequestFailureSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes every row of the log and reports how many were removed.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// <para>
        /// <b>The whole table, whatever the screen was filtered to.</b> The button says "clear the
        /// log", and a delete that obeyed a filter the caller happened not to send would wipe a
        /// different set from the one it named.
        /// </para>
        /// <para>
        /// <b><c>ExecuteDelete</c> is immediate and outside the unit of work</b>, so there is no
        /// save for the service to call and no transaction to roll back: the statement either ran
        /// or it did not. That is the same trade the token stores and the other logs make, and it is
        /// acceptable here because the operation is a single statement whose only outcome is "the
        /// table is empty". An empty table is success with a count of zero, not a refusal - the
        /// second press of a button that already did its job must not read as an error.
        /// </para>
        /// </remarks>
        /// <returns>How many rows were deleted; zero when the table was already empty.</returns>
        Task<int> ClearAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages new failure rows, left for the caller's single save.
        /// </summary>
        /// <param name="failures">Rows to stage, in the order they are given.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// <b>Its only caller is the Development trial seeder.</b> It exists because the rows a
        /// screen is demonstrated with must be written through this layer - a seeder in the
        /// application layer may not reach an internal persistence type - and because the table
        /// has no production write path yet. There is no rule here about which request was refused
        /// or why; that belongs to the future public API.
        /// </remarks>
        /// <returns>The number of rows staged. The caller commits them.</returns>
        Task<int> AddRangeAsync(
            IReadOnlyList<NewApiRequestFailure> failures,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts every row of the log.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// The seeder's idempotence probe: a table that already holds anything is a table the
        /// operator has used, and inserting trial rows over it would mix invented evidence into a
        /// real log.
        /// </remarks>
        Task<int> CountAsync(CancellationToken cancellationToken = default);
    }
}
