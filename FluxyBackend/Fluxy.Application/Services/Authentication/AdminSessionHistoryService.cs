using Fluxy.Core.Authentication.Enums;
using Fluxy.Core.Authentication.Interfaces;
using Fluxy.Core.Authentication.ReadModels;
using Fluxy.Core.Authentication.Repositories;
using Fluxy.Core.Authentication.Rows;
using Fluxy.Core.Authentication.Utils;
using Fluxy.Core.GeoIp.Interfaces;
using Fluxy.Core.GeoIp.ReadModels;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Repositories;
using Fluxy.Core.Users.Utils;

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
        private readonly IUserRepository _users;
        private readonly IRefreshTokenRepository _tokens;
        private readonly ITokenService _tokenService;
        private readonly IGeoIpResolver _geoIp;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminSessionHistoryService"/> class.
        /// </summary>
        /// <param name="users">Repository holding the accounts.</param>
        /// <param name="tokens">Repository holding the token rows.</param>
        /// <param name="tokenService">
        /// Service that ends a session in both halves, so an operator's revocation withdraws
        /// the access tokens as well as the refresh chain.
        /// </param>
        /// <param name="geoIp">Resolver that places each address on the map.</param>
        public AdminSessionHistoryService(
            IUserRepository users,
            IRefreshTokenRepository tokens,
            ITokenService tokenService,
            IGeoIpResolver geoIp)
        {
            _users = users;
            _tokens = tokens;
            _tokenService = tokenService;
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

            // Read as a long rather than an int: a page number taken from a query string can be
            // large enough that `(page - 1) * pageSize` overflows, and a negative offset would
            // reach EF Core as an argument it refuses. Bounding it here means the arithmetic
            // below cannot overflow, and a page past the end simply reads as an empty one.
            var offset = (long)(page - 1) * pageSize;

            // The filter is applied before the count inside the repository, and the visible
            // roles are part of the query rather than a filter over the rows it returns: a
            // total taken over every row would advertise pages whose rows the caller was never
            // going to be shown.
            var result = await _tokens.GetAdminHistoryAsync(
                visible,
                userId,
                offset,
                pageSize,
                search,
                sortBy,
                sortOrder,
                cancellationToken);

            var visits = new List<AdminSessionVisit>(pageSize);

            if (result.Items.Count > 0)
            {
                // One page, one memo of the lookups. A page of visits from one office is the
                // same address twenty times over, and resolving it twenty times buys nothing -
                // the memo is scoped to this read so it cannot outlive the data it describes.
                var geoByAddress = new Dictionary<string, GeoIpInfo>(StringComparer.Ordinal);
                var liveSessions = await _tokens.GetLiveSessionIdsAsync(
                    result.Items.Select(row => row.SessionId).Distinct().ToList(),
                    DateTimeOffset.UtcNow,
                    cancellationToken);

                foreach (var row in result.Items)
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
                Total = result.Total,
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
            var owner = await _tokens.GetSessionOwnerAsync(sessionId, cancellationToken);

            if (owner is null)
            {
                return AdminSessionRevokeOutcome.NotFound;
            }

            // The same scoping the read above applies. An operator who may not read the owner's
            // account must not be able to end its sessions, and must not be able to tell such a
            // session apart from one that never existed - so this is the same word as an unknown
            // identifier, not a permission refusal naming a row the caller was not shown.
            var role = await _users.GetRoleAsync(owner.Value, cancellationToken);

            if (role is not { } ownerRole || !UserPermissionCatalog.CanView(granted, ownerRole))
            {
                return AdminSessionRevokeOutcome.NotFound;
            }

            // Revoked, never deleted, and only where the chain is still worth cutting. The two
            // halves mirror what "live" means everywhere else in this application: spent rows
            // are history and are left alone, and a token that quietly expired while nothing
            // happened is not a session that can still be refreshed.
            //
            // Through the token service rather than the repository, so an operator's word ends
            // both halves of the session: the refresh chain is revoked in the database and every
            // access token the session held is withdrawn at once. The ownership above is
            // repeated in the service's predicate, which is the same answer read twice within
            // one request rather than a second authority on the question.
            var revoked = await _tokenService.RevokeUserSessionAsync(
                owner.Value,
                sessionId,
                cancellationToken);

            return revoked
                ? AdminSessionRevokeOutcome.Revoked
                : AdminSessionRevokeOutcome.NotFound;
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
            StoredAdminSessionVisit row,
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
    }
}
