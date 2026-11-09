using Fluxy.Core.Users.Commands;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.ReadModels;

namespace Fluxy.Core.Users.Interfaces
{
    /// <summary>
    /// Writes the user audit log, reads it a page at a time, and clears it on an administrator's
    /// behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="RecordAsync"/> is the one write path.</b> Every interested flow - registration,
    /// sign-in, password reset, the profile changes, logout - asks this service to record what it
    /// just did, and no caller stages a log row itself. That is what keeps the description's shape,
    /// the GeoIP snapshot and the column truncation in one place.
    /// </para>
    /// <para>
    /// <b>A log write never changes the outcome of the operation it describes.</b> The service
    /// resolves the network snapshot, stages the row and commits it with its own unit of work, and
    /// swallows a failure after logging a warning: an audit trail is a record of what happened, not
    /// a precondition for it happening. Refusing a successful sign-in because the log table was
    /// unreachable would turn an observability feature into an outage.
    /// </para>
    /// <para>
    /// <b>The read side is thin and deliberately so.</b> Every rule it has is either a bound
    /// <see cref="Utils.UserLogQuery"/> refuses before the call or a predicate the repository
    /// translates into SQL. What is left is the delegation that keeps the API naming a Core
    /// contract instead of a repository.
    /// </para>
    /// </remarks>
    public interface IUserLogService
    {
        /// <summary>
        /// Stages and commits one log entry, swallowing a failure after logging it.
        /// </summary>
        /// <param name="entry">
        /// What happened: the account when there is one, the record type, the outcome, the
        /// description and the raw network values of the request.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// The commit is this method's own, not the caller's: the flows that record a log line have
        /// already committed their business change, and a log row that shared their transaction
        /// would either fail with them or hold them open for an optional write.
        /// </remarks>
        Task RecordAsync(NewUserLog entry, CancellationToken cancellationToken = default);

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
        /// <returns>How many rows were deleted; zero when the table was already empty.</returns>
        Task<int> ClearAllAsync(CancellationToken cancellationToken = default);
    }
}
