using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Interfaces;
using Fluxy.Core.Users.ReadModels;
using Fluxy.Core.Users.Repositories;

namespace Fluxy.Application.Users.Services
{
    /// <summary>
    /// Reads the reseller credit log and wipes it on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A thin service, and deliberately so: every rule this feature has is either a bound the
    /// transport refuses before the call or a predicate the repository translates into SQL. What
    /// is left here is the one decision that is neither - that clearing the log is a whole-table
    /// operation rather than a filtered one - and the delegation that keeps the API naming a Core
    /// contract instead of a repository.
    /// </para>
    /// <para>
    /// <b>Nothing here writes a log row.</b> Entries are staged by the admin account service, in
    /// the same save as the balance they describe. This service reads and clears; a second public
    /// path that could write one would let the story change without the balance changing.
    /// </para>
    /// </remarks>
    public sealed class CreditLogService : ICreditLogService
    {
        private readonly ICreditLogRepository _logs;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreditLogService"/> class.
        /// </summary>
        /// <param name="logs">Repository holding the credit log.</param>
        public CreditLogService(ICreditLogRepository logs)
        {
            _logs = logs;
        }

        /// <inheritdoc />
        public Task<CreditLogPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? resellerId,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            CreditLogSortField sortBy,
            CreditLogSortOrder sortOrder,
            CancellationToken cancellationToken = default)
            => _logs.GetPageAsync(
                page,
                pageSize,
                search,
                resellerId,
                dateFrom,
                dateTo,
                sortBy,
                sortOrder,
                cancellationToken);

        /// <inheritdoc />
        public Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
            // No filter is passed because there is none to pass: the button says "clear all logs",
            // and a delete that obeyed the screen's filters would remove a different set from the
            // one the caller could see it naming. An already-empty table answers zero rather than
            // a refusal - the second press of a button that already did its job is not an error.
            => _logs.ClearAllAsync(cancellationToken);
    }
}
