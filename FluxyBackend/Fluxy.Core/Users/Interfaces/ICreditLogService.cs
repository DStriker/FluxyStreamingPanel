using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.ReadModels;

namespace Fluxy.Core.Users.Interfaces
{
    /// <summary>
    /// Reads the reseller credit log and clears it on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A thin service, and deliberately so: every rule this feature has on the read side is either
    /// a bound the transport refuses before the call or a predicate the repository translates into
    /// SQL. What is left here is the one decision that is neither - that clearing the log is a
    /// whole-table operation rather than a filtered one - and the delegation that keeps the API
    /// naming a Core contract instead of a repository.
    /// </para>
    /// <para>
    /// <b>Nothing here writes a log row.</b> Entries are written by the admin account service, in
    /// the same save as the balance they describe; a second public path that could write one would
    /// be a way to change the story without changing the balance.
    /// </para>
    /// </remarks>
    public interface ICreditLogService
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
        /// <returns>How many rows were deleted; zero when the table was already empty.</returns>
        Task<int> ClearAllAsync(CancellationToken cancellationToken = default);
    }
}
