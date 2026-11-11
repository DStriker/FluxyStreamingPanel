using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.ReadModels;
using Fluxy.Core.Subscriptions.Repositories;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IMagEventRepository"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The page is one query over the journal.</b> Every filter except the device search is a
    /// column of <c>mag_events</c> itself, so <c>total</c> and the page are cut from the same
    /// predicate and the same index can answer both. The device address is resolved by a join after
    /// the count, for the page only.
    /// </para>
    /// <para>
    /// <b>The search is a correlated existence check, not a join in the predicate.</b> It covers the
    /// event's own message and the subscription's <c>mag_mac</c>, and the <c>EXISTS</c> shape keeps
    /// <c>total</c> counted on <c>mag_events</c> alone - the same shape the connection log uses for
    /// its owner filter.
    /// </para>
    /// <para>
    /// <b><c>total</c> is counted under the same predicate the page is cut from, before
    /// <c>Skip</c>/<c>Take</c>.</b> Counting after paging would describe the page instead of the
    /// list and make the pager lie.
    /// </para>
    /// </remarks>
    internal sealed class MagEventRepository : IMagEventRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="MagEventRepository"/> class.
        /// </summary>
        /// <param name="context">Context the rows are read from and staged in.</param>
        public MagEventRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<MagEventPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            MagEventType? eventType,
            Guid? subscriptionId,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            MagEventSortField sortBy,
            MagEventSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            var query = _context.MagEvents.AsNoTracking();

            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                // The message is this table's own text; the device's MAC lives on the subscription
                // and is reached through a correlated EXISTS so the count stays over one table.
                query = query.Where(magEvent =>
                    (magEvent.Message != null && magEvent.Message.ToLower().Contains(needle))
                    || _context.Subscriptions.Any(subscription =>
                        subscription.Id == magEvent.SubscriptionId
                        && subscription.MagMac != null
                        && subscription.MagMac.ToLower().Contains(needle)));
            }

            if (eventType is { } wantedEvent)
            {
                query = query.Where(magEvent => magEvent.EventType == wantedEvent);
            }

            if (subscriptionId is { } subscription)
            {
                query = query.Where(magEvent => magEvent.SubscriptionId == subscription);
            }

            // Both bounds are inclusive and either one may be absent. Inclusive because the control
            // names whole days: a `to` bound read as exclusive would drop the rows written during
            // the very day the operator picked. A later `from` than `to` is not a conflict - it is
            // a predicate nothing satisfies, and the page is empty.
            if (dateFrom is { } from)
            {
                query = query.Where(magEvent => magEvent.CreatedAt >= from);
            }

            if (dateTo is { } to)
            {
                query = query.Where(magEvent => magEvent.CreatedAt <= to);
            }

            // Counted under the same predicate the page is cut from, and before the order is
            // applied: a total counted after Skip/Take would describe the page, not the list.
            var total = await query.CountAsync(cancellationToken);

            // CreatedAt is the only sortable column, and both orders end with the identifier: two
            // events can be recorded in the same instant, and without a tie-break the database is
            // free to answer a different order on every read, which would make a page turn show a
            // row twice or skip one. `sortBy` needs no branch here because MagEventSortField has
            // exactly one member.
            var descending = sortOrder == MagEventSortOrder.Desc;

            var ordered = descending
                ? query.OrderByDescending(magEvent => magEvent.CreatedAt)
                    .ThenByDescending(magEvent => magEvent.Id)
                : query.OrderBy(magEvent => magEvent.CreatedAt)
                    .ThenBy(magEvent => magEvent.Id);

            var pageRows = await ordered
                // Arithmetic in long: page comes from a query string, so (page - 1) * pageSize can
                // overflow int and reach EF Core as a negative Skip.
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .Select(magEvent => new
                {
                    magEvent.Id,
                    magEvent.SubscriptionId,
                    magEvent.EventType,
                    magEvent.Message,
                    magEvent.Status,
                    magEvent.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var subscriptionIds = pageRows
                .Select(row => row.SubscriptionId)
                .Distinct()
                .ToList();

            // The device address is resolved for the page only, in one keyed read: an event whose
            // subscription has no MAC reads as null rather than failing the page.
            var macs = subscriptionIds.Count == 0
                ? new Dictionary<Guid, string?>()
                : await _context.Subscriptions
                    .AsNoTracking()
                    .Where(subscription => subscriptionIds.Contains(subscription.Id))
                    .Select(subscription => new { subscription.Id, subscription.MagMac })
                    .ToDictionaryAsync(
                        row => row.Id,
                        row => row.MagMac,
                        cancellationToken);

            var items = pageRows
                .Select(row => new MagEventListItem
                {
                    Id = row.Id,
                    SubscriptionId = row.SubscriptionId,
                    MagMac = macs.TryGetValue(row.SubscriptionId, out var mac) ? mac : null,
                    EventType = row.EventType,
                    Message = row.Message,
                    Status = row.Status,
                    CreatedAt = row.CreatedAt
                })
                .ToList();

            return new MagEventPage
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public async Task<Guid> AddAsync(
            MagEventWrite magEvent,
            CancellationToken cancellationToken = default)
        {
            // Staged, not saved: the caller owns the single save - the rule every repository here
            // follows. The identifier is generated by the row and the audit columns are the
            // context's to stamp, exactly as they are for every other entity.
            var row = MagEventEntity.FromWrite(magEvent);

            await _context.MagEvents.AddAsync(row, cancellationToken);

            return row.Id;
        }

        /// <inheritdoc />
        public async Task<long> ClearAllAsync(CancellationToken cancellationToken = default)
            // Immediate, and deliberately outside the unit of work: one statement whose only
            // outcome is "the table is empty". There is nothing to roll back and nothing for a
            // save to commit. Its remarks in the interface say the same at length.
            //
            // No predicate: the button says "clear the journal", and a delete that obeyed a filter
            // the caller happened not to send would wipe a different set from the one it named.
            => await _context.MagEvents.ExecuteDeleteAsync(cancellationToken);

        /// <inheritdoc />
        public Task<string?> GetMagMacAsync(
            Guid subscriptionId,
            CancellationToken cancellationToken = default)
            => _context.Subscriptions
                .AsNoTracking()
                .Where(subscription => subscription.Id == subscriptionId)
                .Select(subscription => subscription.MagMac)
                .FirstOrDefaultAsync(cancellationToken);

        /// <inheritdoc />
        public Task<bool> SubscriptionExistsAsync(
            Guid subscriptionId,
            CancellationToken cancellationToken = default)
            => _context.Subscriptions
                .AsNoTracking()
                .AnyAsync(subscription => subscription.Id == subscriptionId, cancellationToken);
    }
}
