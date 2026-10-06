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
    /// Reads the active sessions of an account from the rows its own sessions already wrote.
    /// </summary>
    /// <remarks>
    /// There is no table of sessions because there does not need to be one. Every sign-in and
    /// every refresh already stores the address it came from, the user agent it came with and
    /// the moment it happened - that is what <c>refresh_tokens</c> exists to record so that a
    /// session can be traced back to where it was opened. Reading those rows is the whole
    /// implementation: writing a second table would mean two sources for one fact, and the day
    /// they disagree the list is worth nothing.
    ///
    /// One row per <b>session</b> rather than one per token, which is the deliberate opposite of
    /// <see cref="SessionHistoryService"/>. The history page lists every rotation so a network
    /// change is visible; this page lists what is still signed in so a person can end what they
    /// do not recognise - and a rotation is not a second session, it is the same one still
    /// running. The row describes the freshest live token of each session: after a rotation the
    /// previous token is revoked in the same save, so exactly one live row per session is what
    /// the chain looks like between rotations, and "freshest" is the address and client the
    /// session was last seen with.
    ///
    /// Live means <c>revoked_at</c> is null <b>and</b> <c>expires_at</c> is still in the future.
    /// The second half matters: the cleanup that removes expired rows runs during issuance and
    /// rotation, so a token that quietly expired while nothing else happened is still a row -
    /// and a session whose chain holds only such a row cannot be refreshed any more, which is
    /// the definition of not active. Filtering on <c>expires_at</c> here means the list does
    /// not depend on when cleanup last ran.
    ///
    /// GeoIP is resolved at read time rather than stored, like the history: the lookup is a
    /// local memory mapped file, so a list costs one cheap probe per distinct address instead
    /// of a column that has to be filled correctly on the write path and would be wrong for
    /// every address the database learned about later.
    /// </remarks>
    public sealed class ActiveSessionService : IActiveSessionService
    {
        private readonly FluxyDbContext _context;
        private readonly IGeoIpResolver _geoIp;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActiveSessionService"/> class.
        /// </summary>
        /// <param name="context">Context holding the accounts and their tokens.</param>
        /// <param name="geoIp">Resolver that places each address on the map.</param>
        public ActiveSessionService(FluxyDbContext context, IGeoIpResolver geoIp)
        {
            _context = context;
            _geoIp = geoIp;
        }

        /// <inheritdoc />
        public async Task<ActiveSessionList?> GetActiveAsync(
            Guid userId,
            Guid? currentSessionId,
            CancellationToken cancellationToken = default)
        {
            // The same gate every read of an account applies. A blocked account is not handed a
            // profile, a history, or a list of the networks its live sessions sit on: the list
            // names exactly what a blocked account should stop revealing.
            var isActive = await _context.Users
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Id == userId && entry.Status == UserStatus.Registered,
                    cancellationToken);

            if (!isActive)
            {
                return null;
            }

            var now = DateTimeOffset.UtcNow;

            // The live rows only. `RevokedAt == null` keeps spent and ended chains out, and
            // `ExpiresAt > now` keeps a chain that quietly expired out as well - see the class
            // remarks for why the second half cannot be left to the cleanup.
            //
            // Read into memory rather than grouped in the database: the rows this admits are
            // one per active session (a rotation revokes the row it replaces in the same save),
            // so the account holds a handful of them, and the GeoIP descriptions below are
            // built per row anyway - a grouping would save nothing and cost a translation the
            // provider may or may not support.
            var rows = await _context.RefreshTokens
                .AsNoTracking()
                .Where(token =>
                    token.UserId == userId &&
                    token.RevokedAt == null &&
                    token.ExpiresAt > now)
                .OrderByDescending(token => token.CreatedAt)
                .ThenByDescending(token => token.Id)
                .Select(token => new StoredSession
                {
                    Id = token.SessionId,
                    ClientAddress = token.ClientAddress,
                    UserAgent = token.UserAgent,
                    LastSeenAt = token.CreatedAt
                })
                .ToListAsync(cancellationToken);

            // One list, one memo of the lookups. Several sessions from one home connection are
            // the same address several times over, and resolving it several times buys nothing
            // - the memo is scoped to this read so it cannot outlive the data it describes.
            var geoByAddress = new Dictionary<string, GeoIpInfo>(StringComparer.Ordinal);
            var sessions = new List<ActiveSession>(rows.Count);

            foreach (var row in rows)
            {
                sessions.Add(await DescribeAsync(row, currentSessionId, geoByAddress, cancellationToken));
            }

            // The query already ordered by the moment, so the order survives the walk above -
            // but it is stated here rather than relied on twice, because a list someone scans
            // for a session they do not recognise must not depend on the loop preserving the
            // order the database returned. The id is the tie-break for the same reason the
            // history's is: two tokens written in one transaction share a stamp.
            sessions.Sort((one, other) =>
            {
                var byMoment = other.LastSeenAt.CompareTo(one.LastSeenAt);
                return byMoment != 0 ? byMoment : other.Id.CompareTo(one.Id);
            });

            return new ActiveSessionList { Sessions = sessions };
        }

        /// <summary>
        /// Turns one stored row into what the list is shown: its address placed by the GeoIP
        /// database, and its session marked when it is the one being read from.
        /// </summary>
        /// <remarks>
        /// The GeoIP call never throws and never answers null, so a database that is absent or a
        /// private address simply produces three nulls and a list that still reads correctly -
        /// an unknown network is a fact to display, not a reason to fail the whole list.
        /// </remarks>
        private async Task<ActiveSession> DescribeAsync(
            StoredSession row,
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

            return new ActiveSession
            {
                Id = row.Id,
                Ip = geo.Ip ?? row.ClientAddress,
                CountryCode = geo.CountryCode,
                AutonomousSystemNumber = geo.AutonomousSystemNumber,
                Organization = geo.Organization,
                UserAgent = row.UserAgent,
                LastSeenAt = row.LastSeenAt,
                IsCurrent = currentSessionId is { } session && row.Id == session
            };
        }

        /// <summary>
        /// The five columns a session is described from, projected in the query so that no
        /// entity is materialized and no unrelated column is read off the row.
        /// </summary>
        /// <remarks>
        /// A type rather than an anonymous one because a private method has to name it: an
        /// anonymous type cannot be passed as an argument, and this projection is the point
        /// where the query stops being a query and becomes a description.
        /// </remarks>
        private sealed class StoredSession
        {
            public Guid Id { get; init; }

            public string? ClientAddress { get; init; }

            public string? UserAgent { get; init; }

            public DateTimeOffset LastSeenAt { get; init; }
        }
    }
}
