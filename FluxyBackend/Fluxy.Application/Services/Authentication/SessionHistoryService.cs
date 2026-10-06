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
    /// Reads the visit history of an account from the rows its own sessions already wrote.
    /// </summary>
    /// <remarks>
    /// There is no table of visits because there does not need to be one. Every sign-in and
    /// every refresh already stores the address it came from, the user agent it came with and
    /// the moment it happened - that is what <c>refresh_tokens</c> exists to record so that a
    /// session can be traced back to where it was opened. Reading those rows is the whole
    /// implementation: writing a second table would mean two sources for one fact, and the day
    /// they disagree the history is worth nothing.
    ///
    /// One row per token rather than one per session, deliberately. A rotation is a visit: a
    /// browser that moved from one network to another between two refreshes must show up twice,
    /// because "a sign-in I do not recognise" and "a rotation I do not recognise" are the same
    /// alarm and a visitor needs to see both.
    ///
    /// GeoIP is resolved at read time rather than stored. The lookup is a local memory mapped
    /// file, so a page costs one cheap probe per distinct address instead of a column that has
    /// to be filled correctly on the write path and would be wrong for every address the
    /// database learned about later.
    /// </remarks>
    public sealed class SessionHistoryService : ISessionHistoryService
    {
        private readonly FluxyDbContext _context;
        private readonly IGeoIpResolver _geoIp;

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionHistoryService"/> class.
        /// </summary>
        /// <param name="context">Context holding the accounts and their tokens.</param>
        /// <param name="geoIp">Resolver that places each address on the map.</param>
        public SessionHistoryService(FluxyDbContext context, IGeoIpResolver geoIp)
        {
            _context = context;
            _geoIp = geoIp;
        }

        /// <inheritdoc />
        public async Task<SessionHistoryPage?> GetHistoryAsync(
            Guid userId,
            int page,
            int pageSize,
            string? search,
            SessionSortField sortBy,
            SessionSortOrder sortOrder,
            Guid? currentSessionId,
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, SessionHistoryLimits.MaxPageSize);

            // The same gate every read of an account applies. A blocked account is not handed a
            // profile and is not handed a history either: the list names the networks its
            // sessions came from, which is exactly what a blocked account should stop revealing.
            var isActive = await _context.Users
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Id == userId && entry.Status == UserStatus.Registered,
                    cancellationToken);

            if (!isActive)
            {
                return null;
            }

            var query = _context.RefreshTokens
                .AsNoTracking()
                .Where(token => token.UserId == userId);

            // The filter is built before the count, and that ordering is the whole reason search
            // is server side rather than a client-side trim of the rows already on screen. Counting
            // everything and then filtering a page would answer a pager with 200 and then hand it
            // five rows, three times over - a total that knows nothing about the filter is worse
            // than no total, because the pager believes it.
            //
            // Only the stored columns are searched. Country and provider are resolved below while
            // the page is built, so filtering on them here would mean resolving every row an
            // account has before one page of them could be shown.
            if (!string.IsNullOrWhiteSpace(search))
            {
                // Lowered on both sides rather than compared with a case-insensitive operator:
                // this is one account's own rows, the needle is already short, and keeping the
                // expression provider-neutral means this service does not have to know which
                // database it is reading from.
                var needle = search.Trim().ToLowerInvariant();

                query = query.Where(token =>
                    (token.ClientAddress != null && token.ClientAddress.ToLower().Contains(needle)) ||
                    (token.UserAgent != null && token.UserAgent.ToLower().Contains(needle)));
            }

            var total = await query.CountAsync(cancellationToken);

            // Read as a long rather than an int: a page number taken from a query string can be
            // large enough that `(page - 1) * pageSize` overflows, and a negative offset would
            // reach EF Core as an argument it refuses. Bounding it here means the arithmetic
            // below cannot overflow, and a page past the end simply reads as an empty one.
            var offset = (long)(page - 1) * pageSize;

            var visits = new List<SessionVisit>(pageSize);

            if (offset < total)
            {
                var rows = await Sort(query, sortBy, sortOrder)
                    .Skip((int)offset)
                    .Take(pageSize)
                    .Select(token => new StoredVisit
                    {
                        Id = token.Id,
                        SessionId = token.SessionId,
                        ClientAddress = token.ClientAddress,
                        UserAgent = token.UserAgent,
                        VisitedAt = token.CreatedAt
                    })
                    .ToListAsync(cancellationToken);

                // One page, one memo of the lookups. A page of visits from a home connection is
                // the same address twenty times over, and resolving it twenty times buys nothing
                // - the memo is scoped to this read so it cannot outlive the data it describes.
                var geoByAddress = new Dictionary<string, GeoIpInfo>(StringComparer.Ordinal);

                foreach (var row in rows)
                {
                    visits.Add(await DescribeAsync(row, currentSessionId, geoByAddress, cancellationToken));
                }
            }

            return new SessionHistoryPage
            {
                Visits = visits,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <summary>
        /// Orders an account's rows, with a tie-break that makes the order total.
        /// </summary>
        /// <remarks>
        /// Every branch ends in a unique column, because without one two pages can disagree about
        /// rows that compare equal: a row could be shown on both pages or on neither, and the
        /// visitor would have no way to notice. For the moment that tie-break is the identifier -
        /// two tokens written in one transaction share a stamp.
        ///
        /// The address branch puts a missing address last in <b>both</b> directions. Reversing the
        /// comparison would reverse that too, and an account with no stored address on half its
        /// rows would then open with a screenful of blanks - "unknown" is not a value, and a list
        /// somebody is scanning for a network they recognise should not be led by its own holes.
        /// </remarks>
        private static IOrderedQueryable<RefreshTokenEntity> Sort(
            IQueryable<RefreshTokenEntity> query,
            SessionSortField sortBy,
            SessionSortOrder sortOrder)
        {
            var descending = sortOrder == SessionSortOrder.Descending;

            if (sortBy == SessionSortField.Ip)
            {
                return descending
                    ? query.OrderBy(token => token.ClientAddress == null)
                        .ThenByDescending(token => token.ClientAddress)
                    : query.OrderBy(token => token.ClientAddress == null)
                        .ThenBy(token => token.ClientAddress);
            }

            return descending
                ? query.OrderByDescending(token => token.CreatedAt)
                    .ThenByDescending(token => token.Id)
                : query.OrderBy(token => token.CreatedAt)
                    .ThenBy(token => token.Id);
        }

        /// <summary>
        /// Turns one stored row into what the page is shown: its address placed by the GeoIP
        /// database, and its session marked when it is the one being read from.
        /// </summary>
        /// <remarks>
        /// The GeoIP call never throws and never answers null, so a database that is absent or a
        /// private address simply produces three nulls and a list that still reads correctly -
        /// an unknown network is a fact to display, not a reason to fail the whole page.
        /// </remarks>
        private async Task<SessionVisit> DescribeAsync(
            StoredVisit row,
            Guid? currentSessionId,
            Dictionary<string, GeoIpInfo> geoByAddress,
            CancellationToken cancellationToken)
        {
            var key = row.ClientAddress ?? string.Empty;

            if (!geoByAddress.TryGetValue(key, out var geo))
            {
                geo = await _geoIp.ResolveAsync(row.ClientAddress, cancellationToken);
                geoByAddress[key] = geo;
            }

            return new SessionVisit
            {
                Id = row.Id,
                Ip = geo.Ip ?? row.ClientAddress,
                CountryCode = geo.CountryCode,
                AutonomousSystemNumber = geo.AutonomousSystemNumber,
                Organization = geo.Organization,
                UserAgent = row.UserAgent,
                VisitedAt = row.VisitedAt,
                IsCurrent = currentSessionId is { } session && row.SessionId == session
            };
        }

        /// <summary>
        /// The five columns a visit is described from, projected in the query so that no entity
        /// is materialized and no unrelated column is read off the row.
        /// </summary>
        /// <remarks>
        /// A type rather than an anonymous one because a private method has to name it: an
        /// anonymous type cannot be passed as an argument, and this projection is the point where
        /// the query stops being a query and becomes a description.
        /// </remarks>
        private sealed class StoredVisit
        {
            public Guid Id { get; init; }

            public Guid SessionId { get; init; }

            public string? ClientAddress { get; init; }

            public string? UserAgent { get; init; }

            public DateTimeOffset VisitedAt { get; init; }
        }
    }
}
