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
    /// EF Core implementation of <see cref="IConnectionLogRepository"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The page is one query, the titles are one more per medium.</b> The page itself is cut
    /// from the log rows - every filter and the order are columns of that one table - and only
    /// then joined to the subscription and to its owner, because the owner's name is drawn in the
    /// row. The stream title cannot join the same way: it lives in one of two tables depending on
    /// the kind, so it is read as a keyed set for the identifiers this page actually carries and
    /// mapped in memory - the shape the accounts table uses for the visits of its page. That is
    /// also why ordering by the owner or by the title is not offered: both are values the server
    /// only has after the page has been cut.
    /// </para>
    /// <para>
    /// <b>The owner filter is a correlated existence check, not a join in the predicate.</b> It is
    /// the same predicate shape the subscriptions page uses for its computed status, and it keeps
    /// <c>total</c> counted before paging and under exactly the predicate the page is cut from.
    /// </para>
    /// <para>
    /// <b>Nothing here touches <c>IGeoIpResolver</c>.</b> Country and provider are columns, written
    /// when the session opened, and reading a fresh lookup on this path would answer a different
    /// value from the one the filter just ran against.
    /// </para>
    /// </remarks>
    internal sealed class ConnectionLogRepository : IConnectionLogRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionLogRepository"/> class.
        /// </summary>
        /// <param name="context">Context the rows are read from.</param>
        public ConnectionLogRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<ConnectionLogPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            ConnectionStreamKind streamKind,
            bool liveOnly,
            Guid? subscriptionId,
            Guid? ownerId,
            Guid? streamId,
            string? ip,
            string? countryCode,
            string? isp,
            string? userAgent,
            ConnectionLogSortField sortBy,
            ConnectionLogSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            var query = _context.ConnectionLogs.AsNoTracking();

            // The kind is required rather than optional: the screen draws one medium at a time,
            // and a log that folded two unrelated tables' titles into one answer would be a list
            // no screen asks for.
            query = query.Where(log => log.StreamKind == streamKind);

            if (liveOnly)
            {
                query = query.Where(log => log.EndedAt == null);
            }

            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                // Search runs over the three columns a support call quotes - the address, the
                // device and the provider - and over nothing else. Lowering both sides is what
                // the other tables do; the comparison is the database's own collation.
                query = query.Where(log =>
                    log.Ip.ToLower().Contains(needle)
                    || (log.UserAgent != null && log.UserAgent.ToLower().Contains(needle))
                    || (log.Isp != null && log.Isp.ToLower().Contains(needle)));
            }

            if (subscriptionId is { } subscription)
            {
                query = query.Where(log => log.SubscriptionId == subscription);
            }

            if (ownerId is { } owner)
            {
                query = query.Where(log => _context.Subscriptions.Any(row =>
                    row.Id == log.SubscriptionId && row.UserId == owner));
            }

            if (streamId is { } stream)
            {
                query = query.Where(log => log.StreamId == stream);
            }

            if (!string.IsNullOrEmpty(ip))
            {
                query = query.Where(log => log.Ip == ip);
            }

            if (!string.IsNullOrEmpty(countryCode))
            {
                query = query.Where(log => log.CountryCode == countryCode);
            }

            if (!string.IsNullOrEmpty(isp))
            {
                query = query.Where(log => log.Isp == isp);
            }

            // Exact, like the address and the provider beside it, and case included: a user
            // agent is a client's own string, and folding its case would answer rows for a
            // client that never sent it. A null agent matches nothing, which is what an empty
            // table for a filter naming a client the log has never seen means.
            if (!string.IsNullOrEmpty(userAgent))
            {
                query = query.Where(log => log.UserAgent == userAgent);
            }

            // Counted under the same predicate the page is cut from, and before the order is
            // applied: a total counted after Skip/Take would describe the page, not the list.
            var total = await query.CountAsync(cancellationToken);

            query = Order(query, sortBy, sortOrder);

            var pageLogs = query
                // Arithmetic in long: page comes from a query string, so (page - 1) * pageSize can
                // overflow int and reach EF Core as a negative Skip.
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize);

            var rows = await (
                    from log in pageLogs
                    join ownerRow in _context.Subscriptions.AsNoTracking()
                        on log.SubscriptionId equals ownerRow.Id
                    join user in _context.Users.AsNoTracking()
                        on ownerRow.UserId equals user.Id
                    select new
                    {
                        log.Id,
                        log.SubscriptionId,
                        OwnerId = ownerRow.UserId,
                        Username = user.Username,
                        log.StreamKind,
                        log.StreamId,
                        log.Ip,
                        log.UserAgent,
                        log.CountryCode,
                        log.Isp,
                        log.StartedAt,
                        log.EndedAt,
                        log.BroadcastPid
                    })
                .ToListAsync(cancellationToken);

            var titles = await ReadTitlesAsync(
                streamKind,
                rows.Select(row => row.StreamId),
                cancellationToken);

            var items = rows
                .Select(row => new ConnectionLogListItem
                {
                    Id = row.Id,
                    SubscriptionId = row.SubscriptionId,
                    OwnerId = row.OwnerId,
                    Username = row.Username,
                    StreamKind = row.StreamKind,
                    StreamId = row.StreamId,
                    StreamTitle = titles.TryGetValue(row.StreamId, out var title) ? title : null,
                    Ip = row.Ip,
                    UserAgent = row.UserAgent,
                    CountryCode = row.CountryCode,
                    Isp = row.Isp,
                    StartedAt = row.StartedAt,
                    EndedAt = row.EndedAt,
                    BroadcastPid = row.BroadcastPid
                })
                .ToList();

            return new ConnectionLogPage
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
            return _context.ConnectionLogs.ExecuteDeleteAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<int> AddRangeAsync(
            IReadOnlyList<NewConnectionLog> logs,
            CancellationToken cancellationToken = default)
        {
            if (logs.Count == 0)
            {
                return 0;
            }

            // Staged, not saved: the caller owns the single save - the rule every repository
            // here follows. The identifier is generated by the row and the audit columns are
            // the context's to stamp, exactly as they are for every other entity.
            var rows = logs.Select(log => new ConnectionLogEntity
            {
                SubscriptionId = log.SubscriptionId,
                StreamKind = log.StreamKind,
                StreamId = log.StreamId,
                Ip = log.Ip,
                UserAgent = log.UserAgent,
                CountryCode = log.CountryCode,
                Isp = log.Isp,
                StartedAt = log.StartedAt,
                EndedAt = log.EndedAt,
                BroadcastPid = log.BroadcastPid
            });

            await _context.ConnectionLogs.AddRangeAsync(rows, cancellationToken);

            return logs.Count;
        }

        /// <inheritdoc />
        public Task<int> CountAsync(CancellationToken cancellationToken = default)
            // Unfiltered by design: the seeder asks "has anything ever been captured here", not
            // "does this screen have rows".
            => _context.ConnectionLogs.CountAsync(cancellationToken);

        /// <summary>
        /// Reads the titles of one page's streams from the table the kind names, in one query.
        /// </summary>
        /// <param name="streamKind">Which medium's table is read.</param>
        /// <param name="streamIds">Identifiers the page actually carries.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Titles by identifier; an identifier absent from the map has no row any more.</returns>
        private async Task<IReadOnlyDictionary<Guid, string>> ReadTitlesAsync(
            ConnectionStreamKind streamKind,
            IEnumerable<Guid> streamIds,
            CancellationToken cancellationToken)
        {
            var wanted = streamIds.Distinct().ToList();

            if (wanted.Count == 0)
            {
                return new Dictionary<Guid, string>();
            }

            // One IN query per medium rather than a join: the two titles live in two tables, and
            // the page's identifiers are known once the page has been cut. The same shape the
            // accounts table uses for the visits of its page.
            if (streamKind is ConnectionStreamKind.AudioStream)
            {
                return await _context.AudioStreams
                    .AsNoTracking()
                    .Where(stream => wanted.Contains(stream.Id))
                    .Select(stream => new { stream.Id, stream.Name })
                    .ToDictionaryAsync(row => row.Id, row => row.Name, cancellationToken);
            }

            return await _context.VideoStreams
                .AsNoTracking()
                .Where(stream => wanted.Contains(stream.Id))
                .Select(stream => new { stream.Id, stream.Name })
                .ToDictionaryAsync(row => row.Id, row => row.Name, cancellationToken);
        }

        /// <summary>
        /// Orders the page the way the request asked, with the identifier as the tie-break.
        /// </summary>
        /// <remarks>
        /// Both orders end with the identifier, for the reason the other repositories give: two
        /// sessions can start or end in the same instant, and without a tie-break the database is
        /// free to answer a different order on every read, which would make a page turn show a row
        /// twice or skip one.
        ///
        /// An end-time order groups the live sessions - those without an end - last in <b>both</b>
        /// directions: a page opened "most recently ended first" must not lead with rows that have
        /// not ended at all.
        /// </remarks>
        private static IQueryable<ConnectionLogEntity> Order(
            IQueryable<ConnectionLogEntity> query,
            ConnectionLogSortField sortBy,
            ConnectionLogSortOrder sortOrder)
        {
            var descending = sortOrder == ConnectionLogSortOrder.Descending;

            if (sortBy is ConnectionLogSortField.EndedAt)
            {
                var grouped = query.OrderBy(log => log.EndedAt == null);

                return descending
                    ? grouped.ThenByDescending(log => log.EndedAt)
                        .ThenByDescending(log => log.Id)
                    : grouped.ThenBy(log => log.EndedAt)
                        .ThenBy(log => log.Id);
            }

            // StartedAt is the default, and it answers both the untouched screen and an explicit
            // sortBy=startedAt. Newest first is the default order.
            return descending
                ? query.OrderByDescending(log => log.StartedAt)
                    .ThenByDescending(log => log.Id)
                : query.OrderBy(log => log.StartedAt)
                    .ThenBy(log => log.Id);
        }
    }
}
