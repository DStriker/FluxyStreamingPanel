using Fluxy.Core.Abstractions;
using Fluxy.Core.Abstractions.Repositories;
using Fluxy.Core.Models.Authentication;
using Fluxy.Core.Models.GeoIp;
using Fluxy.Core.Models.Users;

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
        private readonly IUserRepository _users;
        private readonly IRefreshTokenRepository _tokens;
        private readonly IGeoIpResolver _geoIp;

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionHistoryService"/> class.
        /// </summary>
        /// <param name="users">Repository holding the accounts.</param>
        /// <param name="tokens">Repository holding the token rows.</param>
        /// <param name="geoIp">Resolver that places each address on the map.</param>
        public SessionHistoryService(
            IUserRepository users,
            IRefreshTokenRepository tokens,
            IGeoIpResolver geoIp)
        {
            _users = users;
            _tokens = tokens;
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
            var isActive = await _users.IsRegisteredAsync(userId, cancellationToken);

            if (!isActive)
            {
                return null;
            }

            // Read as a long rather than an int: a page number taken from a query string can be
            // large enough that `(page - 1) * pageSize` overflows, and a negative offset would
            // reach EF Core as an argument it refuses. Bounding it here means the arithmetic
            // below cannot overflow, and a page past the end simply reads as an empty one.
            var offset = (long)(page - 1) * pageSize;

            // The filter is applied before the count inside the repository, which is the whole
            // reason search is server side rather than a client-side trim of the rows already on
            // screen: a total that knew nothing about the filter would advertise pages the
            // caller was never going to be shown.
            var result = await _tokens.GetHistoryAsync(
                userId,
                offset,
                pageSize,
                search,
                sortBy,
                sortOrder,
                cancellationToken);

            var visits = new List<SessionVisit>(pageSize);

            if (result.Items.Count > 0)
            {
                // One page, one memo of the lookups. A page of visits from a home connection is
                // the same address twenty times over, and resolving it twenty times buys nothing
                // - the memo is scoped to this read so it cannot outlive the data it describes.
                var geoByAddress = new Dictionary<string, GeoIpInfo>(StringComparer.Ordinal);

                foreach (var row in result.Items)
                {
                    visits.Add(await DescribeAsync(row, currentSessionId, geoByAddress, cancellationToken));
                }
            }

            return new SessionHistoryPage
            {
                Visits = visits,
                Total = result.Total,
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
            StoredSessionVisit row,
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
    }
}
