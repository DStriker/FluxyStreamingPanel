using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Authentication;
using Fluxy.Core.Models.GeoIp;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.Application.Services.Authentication
{
    /// <summary>
    /// Reads the visit history of the accounts one operator may see, from the rows their own
    /// sessions already wrote, and ends a session on that operator's word.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There is no table of visits because there does not need to be one - the same reason
    /// <see cref="SessionHistoryService"/> gives, and the same reading of <c>refresh_tokens</c>.
    /// What is different here is the scope: that service reads one account's rows and never has
    /// to ask which accounts exist for it; this one reads the installation's and has to decide
    /// which rows are the operator's business at all. That decision is
    /// <see cref="UserPermissionCatalog.VisibleRoles"/>, taken once and applied to the group of
    /// the account each token belongs to, which is the same rule the accounts list is scoped by
    /// - a person who may see an account may see where it was signed in from, and a person who
    /// may not sees neither.
    /// </para>
    /// <para>
    /// One row per token rather than one per session, deliberately, for the reason
    /// <see cref="SessionVisit"/> gives: a rotation is a visit, and a browser that moved from
    /// the office to home between two refreshes must show up twice. The button beside the row
    /// ends the whole session, because a single link of a refresh chain is not something that
    /// can meaningfully be ended on its own.
    /// </para>
    /// <para>
    /// GeoIP is resolved at read time rather than stored, as everywhere else: the lookup is a
    /// local memory mapped file, so a page costs one cheap probe per distinct address instead of
    /// a column that would have to be filled correctly on the write path and would be wrong for
    /// every address the database learned about later.
    /// </para>
    /// </remarks>
    public sealed class AdminSessionHistoryService : IAdminSessionHistoryService
    {
        private readonly FluxyDbContext _context;
        private readonly IGeoIpResolver _geoIp;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminSessionHistoryService"/> class.
        /// </summary>
        /// <param name="context">Context holding the accounts and their tokens.</param>
        /// <param name="geoIp">Resolver that places each address on the map.</param>
        public AdminSessionHistoryService(FluxyDbContext context, IGeoIpResolver geoIp)
        {
            _context = context;
            _geoIp = geoIp;
        }

        /// <inheritdoc />
        public async Task<AdminSessionHistoryPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? userId,
            SessionSortField sortBy,
            SessionSortOrder sortOrder,
            IReadOnlySet<UserPermission> granted,
            Guid? currentSessionId,
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, SessionHistoryLimits.MaxPageSize);

            // Which accounts this operator may see is decided before the count rather than after
            // the rows, because the pager shows the count: a total taken over every row would
            // advertise pages whose rows the caller was never going to be shown. The coarse
            // policy ahead of this has already refused a caller holding no view permission at
            // all, so this set is not empty by the time it reaches here - and if it somehow is,
            // the inner join below simply matches nothing and the caller is handed a page of
            // zero rows rather than every row of the installation.
            var visible = UserPermissionCatalog.VisibleRoles(granted);

            var query =
                from token in _context.RefreshTokens.AsNoTracking()
                join user in _context.Users.AsNoTracking() on token.UserId equals user.Id
                join accountGroup in _context.UserGroups.AsNoTracking() on user.GroupId equals accountGroup.Id
                where visible.Contains(accountGroup.Role)
                select new StoredVisit
                {
                    Id = token.Id,
                    SessionId = token.SessionId,
                    UserId = user.Id,
                    Username = user.Username,
                    ClientAddress = token.ClientAddress,
                    UserAgent = token.UserAgent,
                    VisitedAt = token.CreatedAt
                };

            if (userId is { } wantedUser)
            {
                query = query.Where(row => row.UserId == wantedUser);
            }

            // The filter is built before the count, and that ordering is the whole reason search
            // is server side rather than a client-side trim of the rows already on screen. Only
            // the stored columns are searched: country and provider are resolved below while the
            // page is built, so filtering on them here would mean resolving every row the
            // operator may see before one page of them could be shown.
            if (!string.IsNullOrWhiteSpace(search))
            {
                // Lowered on both sides rather than compared with a case-insensitive operator:
                // the needle is already short, and keeping the expression provider-neutral means
                // this service does not have to know which database it is reading from.
                var needle = search.Trim().ToLowerInvariant();

                query = query.Where(row =>
                    (row.ClientAddress != null && row.ClientAddress.ToLower().Contains(needle)) ||
                    (row.UserAgent != null && row.UserAgent.ToLower().Contains(needle)));
            }

            var total = await query.CountAsync(cancellationToken);

            // Read as a long rather than an int: a page number taken from a query string can be
            // large enough that `(page - 1) * pageSize` overflows, and a negative offset would
            // reach EF Core as an argument it refuses. Bounding it here means the arithmetic
            // below cannot overflow, and a page past the end simply reads as an empty one.
            var offset = (long)(page - 1) * pageSize;

            var visits = new List<AdminSessionVisit>(pageSize);

            if (offset < total)
            {
                var rows = await Sort(query, sortBy, sortOrder)
                    .Skip((int)Math.Min(offset, int.MaxValue))
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);

                // One page, one memo of the lookups. A page of visits from one office is the
                // same address twenty times over, and resolving it twenty times buys nothing -
                // the memo is scoped to this read so it cannot outlive the data it describes.
                var geoByAddress = new Dictionary<string, GeoIpInfo>(StringComparer.Ordinal);
                var liveSessions = await LiveSessionIdsAsync(rows, cancellationToken);

                foreach (var row in rows)
                {
                    visits.Add(await DescribeAsync(
                        row,
                        currentSessionId,
                        liveSessions.Contains(row.SessionId),
                        geoByAddress,
                        cancellationToken));
                }
            }

            return new AdminSessionHistoryPage
            {
                Visits = visits,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public async Task<AdminSessionRevokeOutcome> RevokeAsync(
            Guid sessionId,
            IReadOnlySet<UserPermission> granted,
            Guid? currentSessionId,
            CancellationToken cancellationToken = default)
        {
            // Before any lookup, and for the same reason <see cref="AdminSessionRevokeOutcome"/>
            // says:
            // the caller named the very session the request is authenticated with. That is a
            // fact about the request rather than about a row, and the caller already knows its
            // own session id, so answering it tells them nothing they could not read from their
            // own token.
            if (currentSessionId is { } current && current == sessionId)
            {
                return AdminSessionRevokeOutcome.CannotRevokeCurrent;
            }

            // The owner of a session, before anything is written. A session belongs to exactly
            // one account, so any row of its chain answers - and reading the chain rather than a
            // separate table is the same decision the whole history rests on.
            var owner = await _context.RefreshTokens
                .AsNoTracking()
                .Where(token => token.SessionId == sessionId)
                .Select(token => new { token.UserId })
                .FirstOrDefaultAsync(cancellationToken);

            if (owner is null)
            {
                return AdminSessionRevokeOutcome.NotFound;
            }

            // The same scoping the read above applies. An operator who may not read the owner's
            // account must not be able to end its sessions, and must not be able to tell such a
            // session apart from one that never existed - so this is the same word as an unknown
            // identifier, not a permission refusal naming a row the caller was not shown.
            var role = await _context.Users
                .AsNoTracking()
                .Where(user => user.Id == owner.UserId)
                .Select(user => (UserRole?)user.Group!.Role)
                .FirstOrDefaultAsync(cancellationToken);

            if (role is not { } ownerRole || !UserPermissionCatalog.CanView(granted, ownerRole))
            {
                return AdminSessionRevokeOutcome.NotFound;
            }

            // Revoked, never deleted, and only where the chain is still worth cutting. The two
            // halves mirror what "live" means everywhere else in this application: spent rows
            // are history and are left alone, and a token that quietly expired while nothing
            // happened is not a session that can still be refreshed.
            //
            // The ownership is already settled above rather than repeated in this predicate,
            // which reads a fraction of a second after it and is not a check on its own - one
            // operator and one session is one transaction's worth of work, and a second read of
            // the owner would be a second chance for the two to disagree.
            var now = DateTimeOffset.UtcNow;

            var revoked = await _context.RefreshTokens
                .Where(token =>
                    token.SessionId == sessionId &&
                    token.RevokedAt == null &&
                    token.ExpiresAt > now)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(token => token.RevokedAt, now),
                    cancellationToken);

            return revoked > 0
                ? AdminSessionRevokeOutcome.Revoked
                : AdminSessionRevokeOutcome.NotFound;
        }

        /// <summary>
        /// Orders the rows, with a tie-break that makes the order total.
        /// </summary>
        /// <remarks>
        /// Every branch ends in a unique column, because without one two pages can disagree
        /// about rows that compare equal: a row could be shown on both pages or on neither, and
        /// the operator would have no way to notice. For the moment that tie-break is the
        /// identifier - two tokens written in one transaction share a stamp.
        ///
        /// The address branch puts a missing address last in <b>both</b> directions. Reversing
        /// the comparison would reverse that too, and an account with no stored address on half
        /// its rows would then open with a screenful of blanks - "unknown" is not a value, and a
        /// list somebody is scanning for a network they recognise should not be led by its own
        /// holes.
        ///
        /// It is the same function <see cref="SessionHistoryService"/> answers with, spelled out
        /// again because the two sort different types and a shared one would have to be generic
        /// over a property the compiler cannot see. What they must not disagree about is the
        /// answer, and that is what the two branches above say.
        /// </remarks>
        private static IOrderedQueryable<StoredVisit> Sort(
            IQueryable<StoredVisit> query,
            SessionSortField sortBy,
            SessionSortOrder sortOrder)
        {
            var descending = sortOrder == SessionSortOrder.Descending;

            if (sortBy == SessionSortField.Ip)
            {
                return descending
                    ? query.OrderBy(row => row.ClientAddress == null)
                        .ThenByDescending(row => row.ClientAddress)
                    : query.OrderBy(row => row.ClientAddress == null)
                        .ThenBy(row => row.ClientAddress);
            }

            return descending
                ? query.OrderByDescending(row => row.VisitedAt)
                    .ThenByDescending(row => row.Id)
                : query.OrderBy(row => row.VisitedAt)
                    .ThenBy(row => row.Id);
        }

        /// <summary>
        /// Which of this page's sessions are still worth ending: one whose chain still holds a
        /// token that is neither revoked nor expired.
        /// </summary>
        /// <remarks>
        /// Answered for the page's own session ids rather than per row, so a session with three
        /// rotations costs one look rather than three - and answered separately from the visit
        /// itself because the two facts have nothing to do with each other: a spent rotation is
        /// a legitimate visit and a perfectly dead session, which is exactly why the page cannot
        /// decide from the freshness of the row it is drawing.
        ///
        /// It also means the list does not depend on when cleanup last ran, the same way the
        /// active sessions list does not: an expired chain is not active however recently its
        /// rows happened to be removed.
        /// </remarks>
        private async Task<IReadOnlySet<Guid>> LiveSessionIdsAsync(
            IReadOnlyList<StoredVisit> rows,
            CancellationToken cancellationToken)
        {
            var result = new HashSet<Guid>();

            if (rows.Count == 0)
            {
                return result;
            }

            var now = DateTimeOffset.UtcNow;
            var wanted = rows.Select(row => row.SessionId).Distinct().ToList();

            var live = await _context.RefreshTokens
                .AsNoTracking()
                .Where(token =>
                    wanted.Contains(token.SessionId) &&
                    token.RevokedAt == null &&
                    token.ExpiresAt > now)
                .Select(token => token.SessionId)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var sessionId in live)
            {
                result.Add(sessionId);
            }

            return result;
        }

        /// <summary>
        /// Turns one stored row into what the page is shown: its address placed by the GeoIP
        /// database, its session marked when it is the one being read from, and its chain
        /// reported when it can still be ended.
        /// </summary>
        /// <remarks>
        /// The GeoIP call never throws and never answers null, so a database that is absent or a
        /// private address simply produces three nulls and a list that still reads correctly -
        /// an unknown network is a fact to display, not a reason to fail the whole page.
        /// </remarks>
        private async Task<AdminSessionVisit> DescribeAsync(
            StoredVisit row,
            Guid? currentSessionId,
            bool isActive,
            Dictionary<string, GeoIpInfo> geoByAddress,
            CancellationToken cancellationToken)
        {
            var key = row.ClientAddress ?? string.Empty;

            if (!geoByAddress.TryGetValue(key, out var geo))
            {
                geo = await _geoIp.ResolveAsync(row.ClientAddress, cancellationToken);
                geoByAddress[key] = geo;
            }

            return new AdminSessionVisit
            {
                Id = row.Id,
                SessionId = row.SessionId,
                UserId = row.UserId,
                Username = row.Username,
                Ip = geo.Ip ?? row.ClientAddress,
                CountryCode = geo.CountryCode,
                AutonomousSystemNumber = geo.AutonomousSystemNumber,
                Organization = geo.Organization,
                UserAgent = row.UserAgent,
                VisitedAt = row.VisitedAt,
                IsCurrent = currentSessionId is { } session && row.SessionId == session,
                IsActive = isActive
            };
        }

        /// <summary>
        /// The columns a visit is described from, projected in the query so that no entity is
        /// materialized and no unrelated column is read off the row.
        /// </summary>
        /// <remarks>
        /// A type rather than an anonymous one because private methods have to name it: an
        /// anonymous type cannot be passed as an argument, and this projection is the point
        /// where the query stops being a query and becomes a description.
        /// </remarks>
        private sealed class StoredVisit
        {
            public Guid Id { get; init; }

            public Guid SessionId { get; init; }

            public Guid UserId { get; init; }

            public string Username { get; init; } = string.Empty;

            public string? ClientAddress { get; init; }

            public string? UserAgent { get; init; }

            public DateTimeOffset VisitedAt { get; init; }
        }
    }
}
