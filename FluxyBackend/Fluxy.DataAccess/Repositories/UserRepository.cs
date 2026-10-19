using System.Linq.Expressions;
using Fluxy.Core.Abstractions.Repositories;
using Fluxy.Core.Models.Authentication;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IUserRepository"/>.
    /// </summary>
    /// <remarks>
    /// Every read that returns an account includes its group, because the model's role, group
    /// name and effective status all come from that row; a repository that returned a model
    /// built without it would have to invent those three values. The standing read is the one
    /// exception, and it projects instead of materializing so no entity exists to map.
    /// </remarks>
    internal sealed class UserRepository : IUserRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserRepository"/> class.
        /// </summary>
        /// <param name="context">Context the account rows are read from and staged in.</param>
        public UserRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<User?> FindByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Users
                .AsNoTracking()
                .Include(entry => entry.Group)
                .FirstOrDefaultAsync(entry => entry.Username == username, cancellationToken);

            return entity?.ToModel();
        }

        /// <inheritdoc />
        public async Task<User?> FindByIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Users
                .AsNoTracking()
                .Include(entry => entry.Group)
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            return entity?.ToModel();
        }

        /// <inheritdoc />
        public async Task<AccountStanding> GetStandingAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            // The projection is the point: one indexed read of an account and its group by
            // primary key, rather than materializing either entity. The password hash and the
            // registration code are not fetched at all, so a mistake that logged this would
            // have far less to leak.
            var row = await _context.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new
                {
                    user.Status,

                    // The null-forgiving operators are about the *declaration*, not about the
                    // database: the foreign key is NOT NULL and RESTRICT, so the join EF builds
                    // for a required relationship cannot leave the group out.
                    GroupStatus = user.Group!.Status,
                    Role = user.Group!.Role,
                    Permissions = user.Group!.Permissions
                        .Select(grant => grant.Permission)
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (row is null)
            {
                // A token that names an account which no longer exists. Status null rather than
                // a state of its own, so the caller checks one thing.
                return new AccountStanding();
            }

            return new AccountStanding
            {
                Status = UserStatusComposition.Combine(row.Status, row.GroupStatus),
                Role = row.Role,
                Permissions = row.Permissions.ToHashSet()
            };
        }

        /// <inheritdoc />
        public Task<bool> IsRegisteredAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
            => _context.Users
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Id == userId && entry.Status == UserStatus.Registered,
                    cancellationToken);

        /// <inheritdoc />
        public Task<bool> HasAdminAsync(CancellationToken cancellationToken = default)
            => _context.Users.AnyAsync(
                user => user.Group!.Role == UserRole.Admin,
                cancellationToken);

        /// <inheritdoc />
        public Task<UserRole?> GetRoleAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
            => _context.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => (UserRole?)user.Group!.Role)
                .FirstOrDefaultAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<StoredProfile?> GetProfileAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Users
                .AsNoTracking()
                .Include(entry => entry.Group)
                .ThenInclude(entry => entry!.Permissions)
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            if (entity is null)
            {
                return null;
            }

            var account = entity.ToModel();

            return new StoredProfile
            {
                Username = account.Username,
                Email = account.Email,
                Role = account.Role,
                Status = account.Status,
                TimeZone = account.TimeZone,
                Permissions = entity.Group!.Permissions
                    .Select(grant => grant.Permission)
                    .ToHashSet()
            };
        }

        /// <inheritdoc />
        public async Task<EditableAccount?> FindEditableByIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Users
                .Include(entry => entry.Group)
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            return ToEditable(entity);
        }

        /// <inheritdoc />
        public async Task<EditableAccount?> FindEditableByEmailAsync(
            string email,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Users
                .Include(entry => entry.Group)
                .FirstOrDefaultAsync(entry => entry.Email == email, cancellationToken);

            return ToEditable(entity);
        }

        /// <inheritdoc />
        public async Task<StoredAccount?> FindForRegistrationByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Users
                .FirstOrDefaultAsync(entry => entry.Username == username, cancellationToken);

            return entity is null ? null : ToStoredAccount(entity);
        }

        /// <inheritdoc />
        public async Task<StoredAccount?> FindForRegistrationByEmailAsync(
            string email,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Users
                .FirstOrDefaultAsync(entry => entry.Email == email, cancellationToken);

            return entity is null ? null : ToStoredAccount(entity);
        }

        /// <inheritdoc />
        public async Task<StoredAccount?> FindForPasswordResetAsync(
            string username,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Users
                .FirstOrDefaultAsync(entry => entry.Username == username, cancellationToken);

            return entity is null ? null : ToStoredAccount(entity);
        }

        /// <inheritdoc />
        public Task<bool> IsUsernameTakenAsync(
            string username,
            Guid? exceptUserId,
            CancellationToken cancellationToken = default)
            => _context.Users
                .AsNoTracking()
                .AnyAsync(
                    entry => (exceptUserId == null || entry.Id != exceptUserId)
                        && entry.Username == username,
                    cancellationToken);

        /// <inheritdoc />
        public Task<bool> IsEmailTakenAsync(
            string email,
            Guid? exceptUserId,
            CancellationToken cancellationToken = default)
            => _context.Users
                .AsNoTracking()
                .AnyAsync(
                    entry => (exceptUserId == null || entry.Id != exceptUserId)
                        && entry.Email == email,
                    cancellationToken);

        /// <inheritdoc />
        public Task<Guid> AddAsync(
            NewAccount account,
            CancellationToken cancellationToken = default)
        {
            var entity = new UserEntity
            {
                Username = account.Username,
                Email = account.Email,
                PasswordHash = account.PasswordHash,
                GroupId = account.GroupId,
                Status = account.Status,
                RegistrationCodeHash = account.RegistrationCodeHash,
                RegistrationCodeExpiresAt = account.RegistrationCodeExpiresAt,
                RegisteredAt = account.RegisteredAt,
                TimeZone = account.TimeZone,
                GeoProtectionEnabled = account.GeoProtectionEnabled,
                BindSessionToIp = account.BindSessionToIp
            };

            _context.Users.Add(entity);

            return Task.FromResult(entity.Id);
        }

        /// <inheritdoc />
        public async Task UpdatePendingRegistrationAsync(
            Guid userId,
            string passwordHash,
            string codeHash,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindTrackedAsync(userId, cancellationToken);

            if (entity is null)
            {
                return;
            }

            entity.PasswordHash = passwordHash;
            entity.RegistrationCodeHash = codeHash;
            entity.RegistrationCodeExpiresAt = expiresAt;
        }

        /// <inheritdoc />
        public async Task ConfirmRegistrationAsync(
            Guid userId,
            DateTimeOffset registeredAt,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindTrackedAsync(userId, cancellationToken);

            if (entity is null)
            {
                return;
            }

            entity.Status = UserStatus.Registered;
            entity.RegisteredAt = registeredAt;
            entity.RegistrationCodeHash = null;
            entity.RegistrationCodeExpiresAt = null;
        }

        /// <inheritdoc />
        public async Task SetUsernameAsync(
            Guid userId,
            string username,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindTrackedAsync(userId, cancellationToken);

            if (entity is not null)
            {
                entity.Username = username;
            }
        }

        /// <inheritdoc />
        public async Task SetEmailAsync(
            Guid userId,
            string email,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindTrackedAsync(userId, cancellationToken);

            if (entity is not null)
            {
                entity.Email = email;
            }
        }

        /// <inheritdoc />
        public async Task SetPasswordHashAsync(
            Guid userId,
            string passwordHash,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindTrackedAsync(userId, cancellationToken);

            if (entity is not null)
            {
                entity.PasswordHash = passwordHash;
            }
        }

        /// <inheritdoc />
        public async Task SetTimeZoneAsync(
            Guid userId,
            string? timeZone,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindTrackedAsync(userId, cancellationToken);

            if (entity is not null)
            {
                entity.TimeZone = timeZone;
            }
        }

        /// <inheritdoc />
        public async Task SetGroupAsync(
            Guid userId,
            Guid groupId,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindTrackedAsync(userId, cancellationToken);

            if (entity is not null)
            {
                entity.GroupId = groupId;
            }
        }

        /// <inheritdoc />
        public async Task SetStatusAsync(
            Guid userId,
            UserStatus status,
            DateTimeOffset? registeredAt,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindTrackedAsync(userId, cancellationToken);

            if (entity is not null)
            {
                entity.Status = status;
                entity.RegisteredAt = registeredAt;
            }
        }

        /// <inheritdoc />
        public async Task SetLoginGuardSwitchesAsync(
            Guid userId,
            bool geoProtectionEnabled,
            bool bindSessionToIp,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindTrackedAsync(userId, cancellationToken);

            if (entity is not null)
            {
                entity.GeoProtectionEnabled = geoProtectionEnabled;
                entity.BindSessionToIp = bindSessionToIp;
            }
        }

        /// <inheritdoc />
        public async Task<AdminUserPage> GetAdminPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? groupId,
            UserStatus? status,
            AdminUserSortField sortBy,
            AdminUserSortOrder sortOrder,
            IReadOnlyCollection<UserRole> visibleRoles,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Users.AsNoTracking();
            query = query.Where(entry => visibleRoles.Contains(entry.Group!.Role));

            if (groupId is { } wantedGroup)
            {
                query = query.Where(entry => entry.GroupId == wantedGroup);
            }

            if (status is { } wantedStatus)
            {
                query = query.Where(EffectiveStatusIs(wantedStatus));
            }

            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                query = query.Where(entry =>
                    entry.Username.ToLower().Contains(needle) ||
                    entry.Email.ToLower().Contains(needle));
            }

            var total = await query.CountAsync(cancellationToken);

            query = Order(query, sortBy, sortOrder);

            var rows = await query
                .Select(entry => new StoredUser
                {
                    Id = entry.Id,
                    Username = entry.Username,
                    Email = entry.Email,
                    GroupId = entry.GroupId,
                    GroupName = entry.Group!.Name,
                    Role = entry.Group!.Role,
                    OwnStatus = entry.Status,
                    GroupStatus = entry.Group.Status
                })
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new AdminUserPage
            {
                Items = await DescribeVisitsAsync(rows, cancellationToken),
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public Task<AdminAccount?> GetAdminAccountAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
            => _context.Users
                .Where(entry => entry.Id == userId)
                .Select(entry => new AdminAccount
                {
                    Id = entry.Id,
                    Username = entry.Username,
                    Email = entry.Email,
                    Status = entry.Status,
                    RegisteredAt = entry.RegisteredAt,
                    GroupId = entry.GroupId,
                    GroupName = entry.Group!.Name,
                    Role = entry.Group!.Role,
                    GroupStatus = entry.Group.Status
                })
                .FirstOrDefaultAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<AdminUserDetail?> GetAdminDetailAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Include(entry => entry.Group)
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            if (user is null)
            {
                return null;
            }

            var rules = await _context.LoginGuardRules
                .AsNoTracking()
                .Where(rule => rule.UserId == userId)
                .ToListAsync(cancellationToken);

            var provider = rules.FirstOrDefault(
                rule => rule.Kind is LoginGuardRuleKind.AutonomousSystem);

            var visit = await LatestVisitAsync(userId, cancellationToken);
            var group = user.Group!;

            return new AdminUserDetail
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = group.Role,
                GroupId = group.Id,
                GroupName = group.Name,
                Status = user.Status,
                EffectiveStatus = UserStatusComposition.Combine(user.Status, group.Status),
                TimeZone = user.TimeZone,
                RegisteredAt = user.RegisteredAt,
                CreatedAt = user.CreatedAt,
                LastSeenAt = visit?.CreatedAt,
                LastIp = visit?.ClientAddress,
                LoginGuard = new LoginGuardSettings
                {
                    GeoProtectionEnabled = user.GeoProtectionEnabled,
                    BindSessionToIp = user.BindSessionToIp,
                    AllowedIps = rules
                        .Where(rule => rule.Kind is LoginGuardRuleKind.IpAddress)
                        .Select(rule => rule.Value)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray(),
                    AllowedCountry = rules
                        .FirstOrDefault(rule => rule.Kind is LoginGuardRuleKind.Country)?.Value,
                    AllowedAutonomousSystemNumber = provider is not null
                        && int.TryParse(provider.Value, out var asn) ? asn : null
                }
            };
        }

        /// <inheritdoc />
        public async Task RemoveAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindTrackedAsync(userId, cancellationToken);

            if (entity is not null)
            {
                _context.Users.Remove(entity);
            }
        }

        /// <summary>
        /// Loads a row that a preceding intent method is about to stage a change on.
        /// </summary>
        /// <remarks>
        /// The row read by the service is already tracked, so this resolves from the change
        /// tracker and issues no second query in the ordinary flow. A row that is no longer
        /// tracked - or was never read - is loaded once rather than assumed to exist.
        /// </remarks>
        private async Task<UserEntity?> FindTrackedAsync(
            Guid userId,
            CancellationToken cancellationToken)
            => await _context.Users.FindAsync([userId], cancellationToken);

        /// <summary>
        /// Reads the freshest token of each account on the page and returns the rows with
        /// their visit attached.
        /// </summary>
        private async Task<List<AdminUser>> DescribeVisitsAsync(
            List<StoredUser> rows,
            CancellationToken cancellationToken)
        {
            var result = rows
                .Select(row => new AdminUser
                {
                    Id = row.Id,
                    Username = row.Username,
                    Email = row.Email,
                    GroupId = row.GroupId,
                    GroupName = row.GroupName,
                    Role = row.Role,
                    Status = UserStatusComposition.Combine(row.OwnStatus, row.GroupStatus)
                })
                .ToList();

            if (rows.Count == 0)
            {
                return result;
            }

            var visits = await LatestVisitsAsync(
                rows.Select(row => row.Id).ToArray(),
                cancellationToken);

            for (var index = 0; index < rows.Count; index++)
            {
                if (visits.TryGetValue(rows[index].Id, out var visit))
                {
                    result[index] = result[index] with
                    {
                        LastSeenAt = visit.CreatedAt,
                        LastIp = visit.ClientAddress
                    };
                }
            }

            return result;
        }

        /// <summary>The freshest token of one account, or null when it has none.</summary>
        private async Task<RefreshTokenEntity?> LatestVisitAsync(
            Guid userId,
            CancellationToken cancellationToken)
            => await _context.RefreshTokens
                .AsNoTracking()
                .Where(token => token.UserId == userId)
                .OrderByDescending(token => token.CreatedAt)
                .ThenByDescending(token => token.Id)
                .FirstOrDefaultAsync(cancellationToken);

        /// <summary>
        /// The freshest token of each of these accounts, keyed by account.
        /// </summary>
        private async Task<IReadOnlyDictionary<Guid, RefreshTokenEntity>> LatestVisitsAsync(
            Guid[] userIds,
            CancellationToken cancellationToken)
        {
            var tokens = await _context.RefreshTokens
                .AsNoTracking()
                .Where(token => userIds.Contains(token.UserId))
                .OrderByDescending(token => token.CreatedAt)
                .ThenByDescending(token => token.Id)
                .ToListAsync(cancellationToken);

            var byAccount = new Dictionary<Guid, RefreshTokenEntity>();
            foreach (var token in tokens)
            {
                if (!byAccount.ContainsKey(token.UserId))
                {
                    byAccount[token.UserId] = token;
                }
            }

            return byAccount;
        }

        /// <summary>
        /// A predicate over the accounts of the page for one effective status, generated from
        /// <see cref="UserStatusComposition.Combine"/> so the rule has one source.
        /// </summary>
        private static Expression<Func<UserEntity, bool>> EffectiveStatusIs(UserStatus wanted)
        {
            var entry = Expression.Parameter(typeof(UserEntity), "entry");
            var own = Expression.Property(entry, nameof(UserEntity.Status));
            var group = Expression.Property(
                Expression.Property(entry, nameof(UserEntity.Group)),
                nameof(UserGroupEntity.Status));

            Expression? body = null;

            foreach (var userStatus in UserStatusComposition.All)
            {
                foreach (var groupStatus in UserStatusComposition.All)
                {
                    if (UserStatusComposition.Combine(userStatus, groupStatus) != wanted)
                    {
                        continue;
                    }

                    var pair = Expression.AndAlso(
                        Expression.Equal(own, Expression.Constant(userStatus, typeof(UserStatus))),
                        Expression.Equal(group, Expression.Constant(groupStatus, typeof(UserStatus))));

                    body = body is null ? pair : Expression.OrElse(body, pair);
                }
            }

            return Expression.Lambda<Func<UserEntity, bool>>(
                body ?? Expression.Constant(false),
                entry);
        }

        /// <summary>Puts the page in the order the caller asked for.</summary>
        private IQueryable<UserEntity> Order(
            IQueryable<UserEntity> query,
            AdminUserSortField field,
            AdminUserSortOrder order)
        {
            var descending = order is AdminUserSortOrder.Descending;

            return field switch
            {
                AdminUserSortField.Email => Chain(
                    query, entry => entry.Email, entry => entry.Username, descending),

                AdminUserSortField.Group => Chain(
                    query, entry => entry.Group!.Name, entry => entry.Username, descending),

                AdminUserSortField.Role => Chain(
                    query, entry => entry.Group!.Role, entry => entry.Username, descending),

                AdminUserSortField.Status => Chain(
                    query, entry => entry.Status, entry => entry.Username, descending),

                AdminUserSortField.LastSeenAt => ChainNullsLast(
                    query, LastSeen(), entry => entry.Username, descending),

                AdminUserSortField.Ip => ChainNullsLast(
                    query, LastVisitAddress(), entry => entry.Username, descending),

                _ => Chain(query, entry => entry.Username, entry => entry.Id, descending)
            };
        }

        private static IOrderedQueryable<UserEntity> Chain<TKey1, TKey2>(
            IQueryable<UserEntity> source,
            Expression<Func<UserEntity, TKey1>> primary,
            Expression<Func<UserEntity, TKey2>> tieBreak,
            bool descending)
        {
            var ordered = descending
                ? source.OrderByDescending(primary)
                : source.OrderBy(primary);

            return descending
                ? ordered.ThenByDescending(tieBreak)
                : ordered.ThenBy(tieBreak);
        }

        /// <summary>Like <see cref="Chain"/>, except that rows without a value go last.</summary>
        private static IOrderedQueryable<UserEntity> ChainNullsLast<TKey1, TKey2>(
            IQueryable<UserEntity> source,
            Expression<Func<UserEntity, TKey1>> primary,
            Expression<Func<UserEntity, TKey2>> tieBreak,
            bool descending)
        {
            var isNull = Expression.Lambda<Func<UserEntity, bool>>(
                Expression.Equal(
                    primary.Body,
                    Expression.Constant(null, primary.Body.Type)),
                primary.Parameters);

            var withGrouped = source.OrderBy(isNull);

            return descending
                ? withGrouped.ThenByDescending(primary).ThenByDescending(tieBreak)
                : withGrouped.ThenBy(primary).ThenBy(tieBreak);
        }

        private Expression<Func<UserEntity, DateTimeOffset?>> LastSeen()
            => entry => _context.RefreshTokens
                .Where(token => token.UserId == entry.Id)
                .Max(token => (DateTimeOffset?)token.CreatedAt);

        private Expression<Func<UserEntity, string?>> LastVisitAddress()
            => entry => _context.RefreshTokens
                .Where(token => token.UserId == entry.Id)
                .OrderByDescending(token => token.CreatedAt)
                .ThenByDescending(token => token.Id)
                .Select(token => token.ClientAddress)
                .FirstOrDefault();

        private static EditableAccount? ToEditable(UserEntity? entity)
        {
            if (entity is null)
            {
                return null;
            }

            return new EditableAccount
            {
                Account = entity.ToModel(),
                RowStatus = entity.Status,
                GroupStatus = entity.Group!.Status
            };
        }

        private static StoredAccount ToStoredAccount(UserEntity entity)
            => new()
            {
                Id = entity.Id,
                Username = entity.Username,
                Email = entity.Email,
                RowStatus = entity.Status
            };

        /// <summary>A row of the admin list before its visit has been attached.</summary>
        private sealed class StoredUser
        {
            public Guid Id { get; init; } = Guid.Empty;
            public string Username { get; init; } = string.Empty;
            public string Email { get; init; } = string.Empty;
            public Guid GroupId { get; init; } = Guid.Empty;
            public string GroupName { get; init; } = string.Empty;
            public UserRole Role { get; init; }
            public UserStatus OwnStatus { get; init; }
            public UserStatus GroupStatus { get; init; }
        }
    }
}
