using Fluxy.Core.Requests.Commands;
using Fluxy.Core.Requests.Enums;
using Fluxy.Core.Requests.ReadModels;
using Fluxy.Core.Requests.Repositories;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IApiRequestFailureRepository"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The page is one query over one table.</b> Every filter and the order are columns of
    /// <c>api_request_failures</c> itself, and no read path joins a content or subscription table:
    /// the row is a snapshot, so it must answer with what it stored rather than with what the
    /// database could resolve today.
    /// </para>
    /// <para>
    /// <b><c>total</c> is counted under the same predicate the page is cut from, before
    /// <c>Skip</c>/<c>Take</c>.</b> Counting after paging would describe the page instead of the
    /// list and make the pager lie.
    /// </para>
    /// </remarks>
    internal sealed class ApiRequestFailureRepository : IApiRequestFailureRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiRequestFailureRepository"/> class.
        /// </summary>
        /// <param name="context">Context the rows are read from and staged in.</param>
        public ApiRequestFailureRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<ApiRequestFailurePage> GetPageAsync(
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
            CancellationToken cancellationToken = default)
        {
            var query = _context.ApiRequestFailures.AsNoTracking();

            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                // Search runs over the three stored free-text columns - the sentence a support call
                // quotes, the access parameter it names and the content it names. Lowering both
                // sides is what the other tables do; the comparison is the database's own collation.
                // The address, provider and country stay out of it: they have exact filters.
                query = query.Where(failure =>
                    failure.Description.ToLower().Contains(needle)
                    || (failure.SubscriptionData != null
                        && failure.SubscriptionData.ToLower().Contains(needle))
                    || (failure.ContentName != null
                        && failure.ContentName.ToLower().Contains(needle)));
            }

            if (reasons is { Count: > 0 } wantedReasons)
            {
                query = query.Where(failure => wantedReasons.Contains(failure.Reason));
            }

            // The three network filters are the connection log's, exactly: the address and the
            // provider as stored - case included, because the row is a snapshot of what the request
            // sent - and the country already canonicalised by the parser. A null column never
            // matches a bound, which is the honest answer for a filter naming a fact the row never
            // recorded.
            if (!string.IsNullOrEmpty(ip))
            {
                query = query.Where(failure => failure.Ip == ip);
            }

            if (!string.IsNullOrEmpty(countryCode))
            {
                query = query.Where(failure => failure.CountryCode == countryCode);
            }

            if (!string.IsNullOrEmpty(isp))
            {
                query = query.Where(failure => failure.Isp == isp);
            }

            // Both bounds are inclusive and either one may be absent. Inclusive because the
            // control names whole days: a `to` bound read as exclusive would drop the rows
            // written during the very day the operator picked. A later `from` than `to` is not a
            // conflict - it is a predicate nothing satisfies, and the page is empty.
            if (dateFrom is { } from)
            {
                query = query.Where(failure => failure.CreatedAt >= from);
            }

            if (dateTo is { } to)
            {
                query = query.Where(failure => failure.CreatedAt <= to);
            }

            // Counted under the same predicate the page is cut from, and before the order is
            // applied: a total counted after Skip/Take would describe the page, not the list.
            var total = await query.CountAsync(cancellationToken);

            // Both orders end with the identifier: two refusals can be recorded in the same instant
            // or carry the same reason, and without a tie-break the database is free to answer a
            // different order on every read, which would make a page turn show a row twice or skip
            // one.
            var descending = sortOrder == ApiRequestFailureSortOrder.Descending;
            var ordered = sortBy is ApiRequestFailureSortField.Reason
                ? descending
                    ? query.OrderByDescending(failure => failure.Reason)
                    : query.OrderBy(failure => failure.Reason)
                : descending
                    ? query.OrderByDescending(failure => failure.CreatedAt)
                    : query.OrderBy(failure => failure.CreatedAt);

            var orderedWithTieBreak = descending
                ? ordered.ThenByDescending(failure => failure.Id)
                : ordered.ThenBy(failure => failure.Id);

            var rows = await orderedWithTieBreak
                // Arithmetic in long: page comes from a query string, so (page - 1) * pageSize can
                // overflow int and reach EF Core as a negative Skip.
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .Select(failure => new
                {
                    failure.Id,
                    failure.SubscriptionData,
                    failure.ContentKind,
                    failure.ContentId,
                    failure.ContentName,
                    failure.Reason,
                    failure.Ip,
                    failure.Isp,
                    failure.CountryCode,
                    failure.Description,
                    failure.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var items = rows
                .Select(row => new ApiRequestFailureListItem
                {
                    Id = row.Id,
                    SubscriptionData = row.SubscriptionData,
                    ContentKind = row.ContentKind,
                    ContentId = row.ContentId,
                    ContentName = row.ContentName,
                    Reason = row.Reason,
                    Ip = row.Ip,
                    Isp = row.Isp,
                    CountryCode = row.CountryCode,
                    Description = row.Description,
                    CreatedAt = row.CreatedAt
                })
                .ToList();

            return new ApiRequestFailurePage
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
            // No predicate: the button says "clear the log", and a delete that obeyed a filter the
            // caller happened not to send would wipe a different set from the one it named.
            return _context.ApiRequestFailures.ExecuteDeleteAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<int> AddRangeAsync(
            IReadOnlyList<NewApiRequestFailure> failures,
            CancellationToken cancellationToken = default)
        {
            if (failures.Count == 0)
            {
                return 0;
            }

            // Staged, not saved: the caller owns the single save - the rule every repository here
            // follows. The identifier is generated by the row and the audit columns are the
            // context's to stamp, exactly as they are for every other entity.
            var rows = failures.Select(failure => new ApiRequestFailureEntity
            {
                SubscriptionData = failure.SubscriptionData,
                ContentKind = failure.ContentKind,
                ContentId = failure.ContentId,
                ContentName = failure.ContentName,
                Reason = failure.Reason,
                Ip = failure.Ip,
                Isp = failure.Isp,
                CountryCode = failure.CountryCode,
                Description = failure.Description
            });

            await _context.ApiRequestFailures.AddRangeAsync(rows, cancellationToken);

            return failures.Count;
        }

        /// <inheritdoc />
        public Task<int> CountAsync(CancellationToken cancellationToken = default)
            // Unfiltered by design: the seeder asks "has anything ever been captured here", not
            // "does this screen have rows".
            => _context.ApiRequestFailures.CountAsync(cancellationToken);
    }
}
