using Fluxy.Core.Abstractions.Repositories;
using Fluxy.Core.Models.Authentication;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IRefreshTokenRepository"/>.
    /// </summary>
    /// <remarks>
    /// The read side projects into the small records the callers actually draw, so a session
    /// list never carries a token hash and an operator's history never materializes an entity.
    /// The one method that returns a whole row is <see cref="FindByHashAsync"/>, because the
    /// rotation that follows has to stamp that same row and link it to its replacement.
    /// </remarks>
    internal sealed class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="RefreshTokenRepository"/> class.
        /// </summary>
        /// <param name="context">Context the token rows are read from and staged in.</param>
        public RefreshTokenRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<StoredRefreshToken?> FindByHashAsync(
            string tokenHash,
            CancellationToken cancellationToken = default)
        {
            // Tracked, not AsNoTracking: the rotation that follows stamps this row and links it
            // to its replacement, and FindAsync in SpendAsync then resolves it from the change
            // tracker rather than reading the same row twice.
            var entity = await _context.RefreshTokens
                .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

            return entity is null ? null : ToModel(entity);
        }

        /// <inheritdoc />
        public Task AddAsync(
            StoredRefreshToken token,
            CancellationToken cancellationToken = default)
        {
            _context.RefreshTokens.Add(new RefreshTokenEntity(token.Id)
            {
                UserId = token.UserId,
                TokenHash = token.TokenHash,
                SessionId = token.SessionId,
                ExpiresAt = token.ExpiresAt,
                RevokedAt = token.RevokedAt,
                ReplacedById = token.ReplacedById,
                ClientAddress = token.ClientAddress,
                UserAgent = token.UserAgent
            });

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task SpendAsync(
            Guid tokenId,
            Guid replacementId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            // FindAsync consults the change tracker first, so the row FindByHashAsync loaded is
            // stamped without a second SELECT. A missing row is a programming error - the
            // service only spends a token it has just read - and is refused rather than
            // silently ignored.
            var entity = await _context.RefreshTokens.FindAsync([tokenId], cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Refresh token '{tokenId}' was not found when it was being spent.");

            entity.RevokedAt = now;
            entity.ReplacedById = replacementId;
        }

        /// <inheritdoc />
        public Task CleanupExpiredAsync(
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            var cutoff = now.AddDays(-30);

            return _context.RefreshTokens
                .Where(token =>
                    token.ExpiresAt <= now ||
                    (token.RevokedAt != null && token.RevokedAt < cutoff))
                .ExecuteDeleteAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<bool> RevokeSessionAsync(
            Guid sessionId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            // Only the live rows are touched. Rows that are already revoked are left as they
            // are: their timestamps are the record of when they were spent, and overwriting
            // them would destroy the one piece of evidence that distinguishes a spent token
            // from a stolen one.
            var updated = await _context.RefreshTokens
                .Where(token => token.SessionId == sessionId && token.RevokedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(token => token.RevokedAt, now),
                    cancellationToken);

            return updated > 0;
        }

        /// <inheritdoc />
        public async Task<int> RevokeAllSessionsAsync(
            Guid userId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            // Every chain of the account goes at once rather than one at a time - a password
            // change has to leave nothing behind that could be exchanged later.
            return await _context.RefreshTokens
                .Where(token => token.UserId == userId && token.RevokedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(token => token.RevokedAt, now),
                    cancellationToken);
        }

        /// <inheritdoc />
        public async Task<bool> RevokeUserSessionAsync(
            Guid userId,
            Guid sessionId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            // The ownership check is in the predicate rather than in a separate read: a row
            // that does not belong to this account simply does not match, so an unknown
            // session and one that belongs to somebody else get the same answer.
            var updated = await _context.RefreshTokens
                .Where(token =>
                    token.UserId == userId &&
                    token.SessionId == sessionId &&
                    token.RevokedAt == null &&
                    token.ExpiresAt > now)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(token => token.RevokedAt, now),
                    cancellationToken);

            return updated > 0;
        }

        /// <inheritdoc />
        public async Task<int> RevokeAllExceptCurrentAsync(
            Guid userId,
            Guid exceptSessionId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            // The sessions are read before they are revoked rather than counted from the
            // update, because the number reported back is of sessions and the update touches
            // tokens. The two agree today - a rotation revokes the row it replaces in the same
            // save - but counting the distinct ids keeps the answer true even if that stops
            // being the case.
            var sessions = await _context.RefreshTokens
                .Where(token =>
                    token.UserId == userId &&
                    token.SessionId != exceptSessionId &&
                    token.RevokedAt == null &&
                    token.ExpiresAt > now)
                .Select(token => token.SessionId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (sessions.Count == 0)
            {
                return 0;
            }

            // Only the live rows are touched, and only of the sessions just named. A named
            // session holds one live row by construction, so this spends exactly the rows the
            // count above counted.
            await _context.RefreshTokens
                .Where(token =>
                    token.RevokedAt == null &&
                    token.ExpiresAt > now &&
                    sessions.Contains(token.SessionId))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(token => token.RevokedAt, now),
                    cancellationToken);

            return sessions.Count;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<StoredActiveSession>> GetActiveSessionsAsync(
            Guid userId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            // The live rows only. RevokedAt == null keeps spent and ended chains out, and
            // ExpiresAt > now keeps a chain that quietly expired out as well - cleanup runs
            // only during issuance and rotation, so the list cannot depend on when it last ran.
            return await _context.RefreshTokens
                .AsNoTracking()
                .Where(token =>
                    token.UserId == userId &&
                    token.RevokedAt == null &&
                    token.ExpiresAt > now)
                .OrderByDescending(token => token.CreatedAt)
                .ThenByDescending(token => token.Id)
                .Select(token => new StoredActiveSession
                {
                    Id = token.SessionId,
                    ClientAddress = token.ClientAddress,
                    UserAgent = token.UserAgent,
                    LastSeenAt = token.CreatedAt
                })
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<StoredSessionVisitPage> GetHistoryAsync(
            Guid userId,
            long offset,
            int pageSize,
            string? search,
            SessionSortField sortBy,
            SessionSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            var query = _context.RefreshTokens
                .AsNoTracking()
                .Where(token => token.UserId == userId);

            // The filter is built before the count, and that ordering is the whole reason
            // search is server side: a total that knew nothing about the filter would tell a
            // pager to expect pages the caller was never going to be shown. Only the stored
            // columns are searched, because country and provider are resolved while the page
            // is built.
            if (!string.IsNullOrWhiteSpace(search))
            {
                var needle = search.Trim().ToLowerInvariant();

                query = query.Where(token =>
                    (token.ClientAddress != null && token.ClientAddress.ToLower().Contains(needle)) ||
                    (token.UserAgent != null && token.UserAgent.ToLower().Contains(needle)));
            }

            var total = await query.CountAsync(cancellationToken);
            var items = new List<StoredSessionVisit>(pageSize);

            // An offset past the end is a valid empty page rather than a reason to ask the
            // database for rows it does not have.
            if (offset < total)
            {
                items = await Sort(query, sortBy, sortOrder)
                    .Skip((int)offset)
                    .Take(pageSize)
                    .Select(token => new StoredSessionVisit
                    {
                        Id = token.Id,
                        SessionId = token.SessionId,
                        ClientAddress = token.ClientAddress,
                        UserAgent = token.UserAgent,
                        VisitedAt = token.CreatedAt
                    })
                    .ToListAsync(cancellationToken);
            }

            return new StoredSessionVisitPage
            {
                Total = total,
                Items = items
            };
        }

        /// <inheritdoc />
        public async Task<StoredAdminSessionVisitPage> GetAdminHistoryAsync(
            IReadOnlySet<UserRole> visibleRoles,
            Guid? userId,
            long offset,
            int pageSize,
            string? search,
            SessionSortField sortBy,
            SessionSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            // The join is the scope: an operator may read the sessions of the accounts whose
            // group role it may read, and the inner join simply matches nothing for the rest.
            var query =
                from token in _context.RefreshTokens.AsNoTracking()
                let user = token.User!
                where visibleRoles.Contains(user.Group!.Role)
                select new StoredAdminSessionVisit
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

            if (!string.IsNullOrWhiteSpace(search))
            {
                var needle = search.Trim().ToLowerInvariant();

                query = query.Where(row =>
                    (row.ClientAddress != null && row.ClientAddress.ToLower().Contains(needle)) ||
                    (row.UserAgent != null && row.UserAgent.ToLower().Contains(needle)));
            }

            var total = await query.CountAsync(cancellationToken);
            var items = new List<StoredAdminSessionVisit>(pageSize);

            if (offset < total)
            {
                items = await Sort(query, sortBy, sortOrder)
                    .Skip((int)Math.Min(offset, int.MaxValue))
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);
            }

            return new StoredAdminSessionVisitPage
            {
                Total = total,
                Items = items
            };
        }

        /// <inheritdoc />
        public async Task<IReadOnlySet<Guid>> GetLiveSessionIdsAsync(
            IReadOnlyCollection<Guid> sessionIds,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            if (sessionIds.Count == 0)
            {
                return new HashSet<Guid>();
            }

            var wanted = sessionIds.Distinct().ToList();

            var live = await _context.RefreshTokens
                .AsNoTracking()
                .Where(token =>
                    wanted.Contains(token.SessionId) &&
                    token.RevokedAt == null &&
                    token.ExpiresAt > now)
                .Select(token => token.SessionId)
                .Distinct()
                .ToListAsync(cancellationToken);

            return live.ToHashSet();
        }

        /// <inheritdoc />
        public Task<Guid?> GetSessionOwnerAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
            => _context.RefreshTokens
                .AsNoTracking()
                .Where(token => token.SessionId == sessionId)
                .Select(token => (Guid?)token.UserId)
                .FirstOrDefaultAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<bool> RevokeLiveSessionAsync(
            Guid sessionId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            var revoked = await _context.RefreshTokens
                .Where(token =>
                    token.SessionId == sessionId &&
                    token.RevokedAt == null &&
                    token.ExpiresAt > now)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(token => token.RevokedAt, now),
                    cancellationToken);

            return revoked > 0;
        }

        /// <summary>
        /// Orders an account's rows, with a tie-break that makes the order total.
        /// </summary>
        /// <remarks>
        /// Every branch ends in a unique column, because without one two pages can disagree
        /// about rows that compare equal: a row could be shown on both pages or on neither.
        /// The address branch puts a missing address last in <b>both</b> directions, so a list
        /// somebody scans for a network they recognise does not open with a screenful of blanks.
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
        /// Orders the operator's rows, with the same tie-break and the same null placement as
        /// the account's own history.
        /// </summary>
        private static IOrderedQueryable<StoredAdminSessionVisit> Sort(
            IQueryable<StoredAdminSessionVisit> query,
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
        /// Maps one tracked row to the model the caller reads.
        /// </summary>
        private static StoredRefreshToken ToModel(RefreshTokenEntity token)
            => new()
            {
                Id = token.Id,
                UserId = token.UserId,
                SessionId = token.SessionId,
                TokenHash = token.TokenHash,
                ExpiresAt = token.ExpiresAt,
                RevokedAt = token.RevokedAt,
                ReplacedById = token.ReplacedById,
                ClientAddress = token.ClientAddress,
                UserAgent = token.UserAgent,
                CreatedAt = token.CreatedAt
            };
    }
}
