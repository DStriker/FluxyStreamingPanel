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
    /// EF Core implementation of <see cref="ICreditLogRepository"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The page is cut from the log alone, then joined to the two accounts it names.</b>
    /// Search, every filter and the order are columns of <c>credit_logs</c> - except the reseller
    /// username, which the search also matches, so the search is evaluated through the single
    /// required join rather than after the page. Ordering never reaches the usernames, which is
    /// why the two sortable fields are the only ones offered.
    /// </para>
    /// <para>
    /// <b>The issuer's username is a nullable join.</b> A reseller-spent row has no issuer, and a
    /// row whose administrator was deleted has an identifier the foreign key already nulled. Both
    /// answer <c>null</c> rather than a fabricated name, and <c>total</c> is never computed through
    /// that join - only the reseller join, which cannot multiply a row.
    /// </para>
    /// </remarks>
    internal sealed class CreditLogRepository : ICreditLogRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreditLogRepository"/> class.
        /// </summary>
        /// <param name="context">Context the rows are read from and staged in.</param>
        public CreditLogRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<CreditLogPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? resellerId,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            CreditLogSortField sortBy,
            CreditLogSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            // The required join is declared once and used by both the count and the page, so the
            // search cannot describe one set while the rows come from another.
            var query =
                from log in _context.CreditLogs.AsNoTracking()
                join reseller in _context.Users.AsNoTracking()
                    on log.ResellerId equals reseller.Id
                select new { Log = log, ResellerUsername = reseller.Username };

            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                // The same two columns the support call quotes: the sentence the operator reads
                // and the login name it is about. Lowering both sides is what the other tables do;
                // the comparison is the database's own collation.
                query = query.Where(row =>
                    row.Log.Description.ToLower().Contains(needle)
                    || row.ResellerUsername.ToLower().Contains(needle));
            }

            if (resellerId is { } wantedReseller)
            {
                query = query.Where(row => row.Log.ResellerId == wantedReseller);
            }

            // Both bounds are inclusive and either one may be absent. Inclusive because the
            // control names whole days: a `to` bound read as exclusive would drop the rows
            // written during the very day the operator picked.
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

            // Both orders end with the identifier, for the reason the other repositories give:
            // two entries can be written in the same instant or carry the same amount, and
            // without a tie-break the database is free to answer a different order on every read,
            // which would make a page turn show a row twice or skip one. No order reaches a
            // username: both sortable fields are columns of this table.
            var descending = sortOrder == CreditLogSortOrder.Descending;
            var ordered = sortBy is CreditLogSortField.Amount
                ? descending
                    ? query.OrderByDescending(row => row.Log.Amount)
                    : query.OrderBy(row => row.Log.Amount)
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
                    row.Log.ResellerId,
                    row.ResellerUsername,
                    row.Log.IssuedById,
                    row.Log.Amount,
                    row.Log.Description,
                    row.Log.CreatedAt
                })
                .ToListAsync(cancellationToken);

            // The issuer names are resolved for the page alone, in one IN query: a join here would
            // be a second, nullable join in the counted query for a column the filter never uses.
            var issuerIds = rows
                .Where(row => row.IssuedById is not null)
                .Select(row => row.IssuedById!.Value)
                .Distinct()
                .ToList();

            var issuers = issuerIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await _context.Users
                    .AsNoTracking()
                    .Where(user => issuerIds.Contains(user.Id))
                    .Select(user => new { user.Id, user.Username })
                    .ToDictionaryAsync(user => user.Id, user => user.Username, cancellationToken);

            var items = rows
                .Select(row => new CreditLogListItem
                {
                    Id = row.Id,
                    ResellerId = row.ResellerId,
                    ResellerUsername = row.ResellerUsername,
                    IssuedById = row.IssuedById,
                    IssuedByUsername = row.IssuedById is { } issuer
                        && issuers.TryGetValue(issuer, out var username) ? username : null,
                    Amount = row.Amount,
                    Description = row.Description,
                    CreatedAt = row.CreatedAt
                })
                .ToList();

            return new CreditLogPage
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
            // outcome is "the table is empty". There is nothing to roll back and nothing for a
            // save to commit. Its remarks in the interface say the same at length.
            //
            // No predicate: the button says "clear all logs", and a delete that obeyed a filter
            // the caller happened not to send would wipe a different set from the one it named.
            return _context.CreditLogs.ExecuteDeleteAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task AddAsync(NewCreditLog entry, CancellationToken cancellationToken = default)
        {
            // Staged, not saved: the caller stages the balance change beside this row and commits
            // both with one save, which is what makes the two atomic. The identifier is generated
            // by the row and the audit columns are the context's to stamp.
            _context.CreditLogs.Add(new CreditLogEntity
            {
                ResellerId = entry.ResellerId,
                IssuedById = entry.IssuedById,
                Amount = entry.Amount,
                Description = entry.Description
            });

            return Task.CompletedTask;
        }
    }
}
