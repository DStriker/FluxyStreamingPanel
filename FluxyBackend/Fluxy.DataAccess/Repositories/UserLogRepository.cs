using Fluxy.Core.Users.Commands;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.ReadModels;
using Fluxy.Core.Users.Repositories;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IUserLogRepository"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The page is cut from the log, then joined to the account it names.</b> Every filter and
    /// the order are columns of <c>user_logs</c>, except the search, which also matches the login
    /// name and so is evaluated through the single join - the same predicate the count uses, which
    /// is what keeps <c>total</c> describing the page beside it.
    /// </para>
    /// <para>
    /// <b>The account join is <c>LEFT JOIN</c>.</b> A row may have no account at all - an attempt
    /// against an identifier nobody held - and one whose account was deleted has the identifier
    /// nulled by the foreign key. Both answer a null name rather than dropping the row, because the
    /// row is the evidence and the name is only how it is drawn.
    /// </para>
    /// </remarks>
    internal sealed class UserLogRepository : IUserLogRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// The row <see cref="AddAsync"/> staged, kept so a failed save can drop it again.
        /// </summary>
        private UserLogEntity? _staged;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserLogRepository"/> class.
        /// </summary>
        /// <param name="context">Context the rows are read from and staged in.</param>
        public UserLogRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<UserLogPage> GetPageAsync(
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
            CancellationToken cancellationToken = default)
        {
            // The left join is declared once and used by both the count and the page, so the
            // search cannot describe one set while the rows come from another. It cannot multiply
            // a row either: a log entry names at most one account. Only the two columns the read
            // uses are projected, so the join stays a plain LEFT JOIN with no second shape.
            var query =
                from log in _context.UserLogs.AsNoTracking()
                join account in _context.Users.AsNoTracking()
                    on log.UserId equals account.Id into accounts
                from account in accounts.DefaultIfEmpty()
                select new
                {
                    Log = log,
                    Username = account == null ? null : account.Username,
                    GroupId = account == null ? (Guid?)null : account.GroupId
                };

            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                // The two columns the support call quotes: the sentence the operator reads and the
                // login name it is about. Lowering both sides is what the other tables do; the
                // comparison is the database's own collation.
                query = query.Where(row =>
                    row.Log.Description.ToLower().Contains(needle)
                    || (row.Username != null && row.Username.ToLower().Contains(needle)));
            }

            if (userIds is { Count: > 0 } wantedUsers)
            {
                // The nullable column is compared against the filter widened to nullable, so the
                // predicate stays one `IN` over the column - a row with no account simply never
                // matches, which is the same answer the filter means.
                var wantedAsNullable = wantedUsers.Select(id => (Guid?)id).ToList();

                query = query.Where(row => wantedAsNullable.Contains(row.Log.UserId));
            }

            if (groupIds is { Count: > 0 } wantedGroups)
            {
                // Through the account's current group: the filter asks which accounts the operator
                // is looking at, and a group is a property of the account, not of the event. A row
                // with no account belongs to no group and is therefore excluded when a group is
                // named - which is the honest answer, not a silent widening.
                var wantedAsNullable = wantedGroups.Select(id => (Guid?)id).ToList();

                query = query.Where(row => wantedAsNullable.Contains(row.GroupId));
            }

            if (recordTypes is { Count: > 0 } wantedTypes)
            {
                query = query.Where(row => wantedTypes.Contains(row.Log.RecordType));
            }

            // The three network filters are the connection log's, exactly: the address and the
            // provider as stored - case included, because the row is a snapshot of what the request
            // sent - and the country already canonicalised by the parser. A null column never
            // matches a bound, which is the honest answer for a filter naming a fact the row never
            // recorded.
            if (!string.IsNullOrEmpty(ip))
            {
                query = query.Where(row => row.Log.Ip == ip);
            }

            if (!string.IsNullOrEmpty(countryCode))
            {
                query = query.Where(row => row.Log.CountryCode == countryCode);
            }

            if (!string.IsNullOrEmpty(isp))
            {
                query = query.Where(row => row.Log.Isp == isp);
            }

            // Both bounds are inclusive and either one may be absent. Inclusive because the control
            // names whole days: a `to` bound read as exclusive would drop the rows written during
            // the very day the operator picked. A `from` later than `to` is not refused here - the
            // predicate simply admits nothing and the page comes back empty.
            if (dateFrom is { } from)
            {
                query = query.Where(row => row.Log.CreatedAt >= from);
            }

            if (dateTo is { } to)
            {
                query = query.Where(row => row.Log.CreatedAt <= to);
            }

            // Counted under the same predicate the page is cut from, and before the order is
            // applied: a total counted after Skip/Take would describe the page, not the list.
            var total = await query.CountAsync(cancellationToken);

            // Both orders end with the identifier, for the reason the other repositories give: two
            // entries can be written in the same instant or carry the same record type, and without
            // a tie-break the database is free to answer a different order on every read, which
            // would make a page turn show a row twice or skip one. No order reaches the login name:
            // both sortable fields are columns of this table.
            var descending = sortOrder == UserLogSortOrder.Descending;
            var ordered = sortBy is UserLogSortField.RecordType
                ? descending
                    ? query.OrderByDescending(row => row.Log.RecordType)
                    : query.OrderBy(row => row.Log.RecordType)
                : descending
                    ? query.OrderByDescending(row => row.Log.CreatedAt)
                    : query.OrderBy(row => row.Log.CreatedAt);

            var orderedWithTieBreak = descending
                ? ordered.ThenByDescending(row => row.Log.Id)
                : ordered.ThenBy(row => row.Log.Id);

            var rows = await orderedWithTieBreak
                // Arithmetic in long: page comes from a query string, so (page - 1) * pageSize can
                // overflow int and reach EF Core as a negative Skip.
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .Select(row => new
                {
                    row.Log.Id,
                    row.Log.UserId,
                    row.Username,
                    row.Log.RecordType,
                    row.Log.IsSuccess,
                    row.Log.Description,
                    row.Log.Ip,
                    row.Log.Isp,
                    row.Log.CountryCode,
                    row.Log.UserAgent,
                    row.Log.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var items = rows
                .Select(row => new UserLogListItem
                {
                    Id = row.Id,
                    UserId = row.UserId,
                    Username = row.Username,
                    RecordType = row.RecordType,
                    IsSuccess = row.IsSuccess,
                    Description = row.Description,
                    Ip = row.Ip,
                    Isp = row.Isp,
                    CountryCode = row.CountryCode,
                    UserAgent = row.UserAgent,
                    CreatedAt = row.CreatedAt
                })
                .ToList();

            return new UserLogPage
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
        {
            // Immediate, and deliberately outside the unit of work: one statement whose only
            // outcome is "the table is empty". There is nothing to roll back and nothing for a save
            // to commit. Its remarks in the interface say the same at length.
            //
            // No predicate: the button says "clear all logs", and a delete that obeyed a filter the
            // caller happened not to send would wipe a different set from the one it named.
            return _context.UserLogs.ExecuteDeleteAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task AddAsync(NewUserLog entry, CancellationToken cancellationToken = default)
        {
            // Staged, not saved: the service that records an event owns the one save that commits
            // it, which keeps the log write out of its caller's transaction. The identifier is
            // generated by the row and the audit columns are the context's to stamp.
            _staged = new UserLogEntity
            {
                UserId = entry.UserId,
                RecordType = entry.RecordType,
                IsSuccess = entry.IsSuccess,
                Description = entry.Description,
                Ip = entry.Ip,
                Isp = entry.Isp,
                CountryCode = entry.CountryCode,
                UserAgent = entry.UserAgent
            };

            _context.UserLogs.Add(_staged);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void DiscardStaged()
        {
            if (_staged is null)
            {
                return;
            }

            // Detached rather than deleted: the row never reached the database, so there is
            // nothing to delete - it only has to stop the next save of this request from retrying
            // the insert. A save that failed left it in the tracker, which is the whole reason
            // this method exists.
            _context.Entry(_staged).State = EntityState.Detached;
            _staged = null;
        }
    }
}
