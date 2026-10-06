using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Authentication;
using Fluxy.Core.Models.GeoIp;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
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

            var total = await _context.RefreshTokens
                .AsNoTracking()
                .CountAsync(token => token.UserId == userId, cancellationToken);

            // Read as a long rather than an int: a page number taken from a query string can be
            // large enough that `(page - 1) * pageSize` overflows, and a negative offset would
            // reach EF Core as an argument it refuses. Bounding it here means the arithmetic
            // below cannot overflow, and a page past the end simply reads as an empty one.
            var offset = (long)(page - 1) * pageSize;

            var visits = new List<SessionVisit>(pageSize);

            if (offset < total)
            {
                var rows = await _context.RefreshTokens
                    .AsNoTracking()
                    .Where(token => token.UserId == userId)
                    .OrderByDescending(token => token.CreatedAt)
                    // Ties are possible in principle - two tokens written in the same
                    // transaction share a stamp - and without a tie-break two pages could
                    // both show or both skip the same row. The identifier is unique, which
                    // is exactly what a stable order needs.
                    .ThenByDescending(token => token.Id)
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
