using System.Linq.Expressions;
using Fluxy.Core.Abstractions.Repositories;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IUserGroupRepository"/>.
    /// </summary>
    /// <remarks>
    /// The repository owns the two shapes the group screens read - a page of table rows and one
    /// group in full - and the storage of the permission rows. The three refusals that protect
    /// the installation stay in the service: they are rules about what the rows mean, not about
    /// how they are read or written.
    /// </remarks>
    public sealed class UserGroupRepository : IUserGroupRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserGroupRepository"/> class.
        /// </summary>
        /// <param name="context">Context the group rows are read from and staged in.</param>
        public UserGroupRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<UserGroupPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            UserRole? role,
            UserStatus? status,
            UserGroupSortField field,
            UserGroupSortOrder order,
            CancellationToken cancellationToken = default)
        {
            var query = _context.UserGroups.AsNoTracking();

            if (role is { } wantedRole)
            {
                query = query.Where(entry => entry.Role == wantedRole);
            }

            if (status is { } wantedStatus)
            {
                query = query.Where(entry => entry.Status == wantedStatus);
            }

            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                query = query.Where(entry => entry.Name.ToLower().Contains(needle));
            }

            var total = await query.CountAsync(cancellationToken);

            query = Order(query, field, order);

            var rows = await query
                .Select(entry => new StoredGroup
                {
                    Id = entry.Id,
                    Name = entry.Name,
                    Role = entry.Role,
                    Status = entry.Status,
                    PermissionsCount = entry.Permissions.Count
                })
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new UserGroupPage
            {
                Items = rows.Select(ToListItem).ToList(),
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public async Task<UserGroupDetail?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var group = await _context.UserGroups
                .AsNoTracking()
                .Include(entry => entry.Permissions)
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            if (group is null)
            {
                return null;
            }

            var members = await CountMembersAsync(id, cancellationToken);

            return new UserGroupDetail
            {
                Id = group.Id,
                Name = group.Name,
                Role = group.Role,
                Status = group.Status,
                Permissions = group.Permissions
                    .Select(grant => grant.Permission)
                    .ToHashSet(),
                IsBase = BaseUserGroups.IsBase(group.Id),
                Members = members,
                CreatedAt = group.CreatedAt,
                UpdatedAt = group.UpdatedAt
            };
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<UserGroupListItem>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            var rows = await _context.UserGroups
                .AsNoTracking()
                .OrderBy(entry => entry.Name)
                .Select(entry => new StoredGroup
                {
                    Id = entry.Id,
                    Name = entry.Name,
                    Role = entry.Role,
                    Status = entry.Status,
                    PermissionsCount = entry.Permissions.Count
                })
                .ToListAsync(cancellationToken);

            return rows.Select(ToListItem).ToList();
        }

        /// <inheritdoc />
        public Task<UserGroupSummary?> GetSummaryAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => _context.UserGroups
                .AsNoTracking()
                .Where(group => group.Id == id)
                .Select(group => new UserGroupSummary
                {
                    Id = group.Id,
                    Name = group.Name,
                    Role = group.Role,
                    Status = group.Status
                })
                .FirstOrDefaultAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<UserGroup?> FindForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.UserGroups
                .Include(entry => entry.Permissions)
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            return entity is null ? null : ToModel(entity);
        }

        /// <inheritdoc />
        public Task<UserRole?> GetRoleAsync(
            Guid groupId,
            CancellationToken cancellationToken = default)
            => _context.UserGroups
                .AsNoTracking()
                .Where(group => group.Id == groupId)
                .Select(group => (UserRole?)group.Role)
                .FirstOrDefaultAsync(cancellationToken);

        /// <inheritdoc />
        public Task<bool> IsNameTakenAsync(
            string name,
            Guid? exceptGroup,
            CancellationToken cancellationToken = default)
            => _context.UserGroups
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Name == name
                        && (exceptGroup == null || entry.Id != exceptGroup),
                    cancellationToken);

        /// <inheritdoc />
        public Task<Guid> AddAsync(
            NewUserGroup group,
            CancellationToken cancellationToken = default)
        {
            var entity = new UserGroupEntity(group.Id ?? Guid.NewGuid())
            {
                Name = group.Name,
                Role = group.Role,
                Status = group.Status
            };

            _context.UserGroups.Add(entity);
            return Task.FromResult(entity.Id);
        }

        /// <inheritdoc />
        public Task<Guid> AddWithPermissionsAsync(
            NewUserGroup group,
            IReadOnlySet<UserPermission> permissions,
            CancellationToken cancellationToken = default)
        {
            var entity = new UserGroupEntity(group.Id ?? Guid.NewGuid())
            {
                Name = group.Name,
                Role = group.Role,
                Status = group.Status
            };

            foreach (var permission in permissions)
            {
                entity.Permissions.Add(new UserGroupPermissionEntity
                {
                    UserGroupId = entity.Id,
                    Permission = permission
                });
            }

            _context.UserGroups.Add(entity);
            return Task.FromResult(entity.Id);
        }

        /// <inheritdoc />
        public async Task SetNameAsync(
            Guid groupId,
            string name,
            CancellationToken cancellationToken = default)
        {
            var entity = await GetTrackedGroupAsync(groupId, cancellationToken);
            entity.Name = name;
        }

        /// <inheritdoc />
        public async Task SetRoleAsync(
            Guid groupId,
            UserRole role,
            CancellationToken cancellationToken = default)
        {
            var entity = await GetTrackedGroupAsync(groupId, cancellationToken);
            entity.Role = role;
        }

        /// <inheritdoc />
        public async Task SetStatusAsync(
            Guid groupId,
            UserStatus status,
            CancellationToken cancellationToken = default)
        {
            var entity = await GetTrackedGroupAsync(groupId, cancellationToken);
            entity.Status = status;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<UserPermission>> GetPermissionsAsync(
            Guid groupId,
            CancellationToken cancellationToken = default)
            => await _context.GroupPermissions
                .AsNoTracking()
                .Where(grant => grant.UserGroupId == groupId)
                .Select(grant => grant.Permission)
                .ToListAsync(cancellationToken);

        /// <inheritdoc />
        public async Task ReplacePermissionsAsync(
            Guid groupId,
            IReadOnlySet<UserPermission> permissions,
            CancellationToken cancellationToken = default)
        {
            var entity = await GetTrackedGroupAsync(groupId, cancellationToken);

            if (!_context.Entry(entity).Collection(entry => entry.Permissions).IsLoaded)
            {
                await _context.Entry(entity)
                    .Collection(entry => entry.Permissions)
                    .LoadAsync(cancellationToken);
            }

            var stale = entity.Permissions
                .Where(grant => !permissions.Contains(grant.Permission))
                .ToList();

            foreach (var grant in stale)
            {
                entity.Permissions.Remove(grant);
            }

            var held = entity.Permissions
                .Select(grant => grant.Permission)
                .ToHashSet();

            foreach (var permission in permissions)
            {
                if (held.Add(permission))
                {
                    entity.Permissions.Add(new UserGroupPermissionEntity
                    {
                        UserGroupId = entity.Id,
                        Permission = permission
                    });
                }
            }
        }

        /// <inheritdoc />
        public Task AddPermissionAsync(
            Guid groupId,
            UserPermission permission,
            CancellationToken cancellationToken = default)
        {
            _context.GroupPermissions.Add(new UserGroupPermissionEntity
            {
                UserGroupId = groupId,
                Permission = permission
            });

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task RemovePermissionAsync(
            Guid groupId,
            UserPermission permission,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.GroupPermissions
                .FirstOrDefaultAsync(
                    grant => grant.UserGroupId == groupId && grant.Permission == permission,
                    cancellationToken);

            if (entity is not null)
            {
                _context.GroupPermissions.Remove(entity);
            }
        }

        /// <inheritdoc />
        public Task<int> CountMembersAsync(
            Guid groupId,
            CancellationToken cancellationToken = default)
            => _context.Users
                .AsNoTracking()
                .CountAsync(entry => entry.GroupId == groupId, cancellationToken);

        /// <inheritdoc />
        public async Task RemoveAsync(
            Guid groupId,
            CancellationToken cancellationToken = default)
        {
            var entity = await GetTrackedGroupAsync(groupId, cancellationToken);

            if (!_context.Entry(entity).Collection(entry => entry.Permissions).IsLoaded)
            {
                await _context.Entry(entity)
                    .Collection(entry => entry.Permissions)
                    .LoadAsync(cancellationToken);
            }

            _context.UserGroups.Remove(entity);
        }

        /// <summary>Loads the tracked row a change is staged on, or fails loudly.</summary>
        private async Task<UserGroupEntity> GetTrackedGroupAsync(
            Guid groupId,
            CancellationToken cancellationToken)
        {
            var entity = await _context.UserGroups.FindAsync([groupId], cancellationToken);

            return entity ?? throw new InvalidOperationException(
                $"User group '{groupId}' was not found when a change was being staged.");
        }

        /// <summary>Orders by the first key and breaks the tie with the second.</summary>
        private static IQueryable<UserGroupEntity> Order(
            IQueryable<UserGroupEntity> query,
            UserGroupSortField field,
            UserGroupSortOrder order)
        {
            var descending = order is UserGroupSortOrder.Descending;

            return field switch
            {
                UserGroupSortField.Role => Chain(
                    query, entry => entry.Role, entry => entry.Name, descending),

                UserGroupSortField.Status => Chain(
                    query, entry => entry.Status, entry => entry.Name, descending),

                UserGroupSortField.PermissionsCount => Chain(
                    query, entry => entry.Permissions.Count, entry => entry.Name, descending),

                UserGroupSortField.CreatedAt => Chain(
                    query, entry => entry.CreatedAt, entry => entry.Name, descending),

                _ => Chain(query, entry => entry.Name, entry => entry.Id, descending)
            };
        }

        private static IOrderedQueryable<UserGroupEntity> Chain<TKey1, TKey2>(
            IQueryable<UserGroupEntity> source,
            Expression<Func<UserGroupEntity, TKey1>> primary,
            Expression<Func<UserGroupEntity, TKey2>> tieBreak,
            bool descending)
        {
            var ordered = descending
                ? source.OrderByDescending(primary)
                : source.OrderBy(primary);

            return descending
                ? ordered.ThenByDescending(tieBreak)
                : ordered.ThenBy(tieBreak);
        }

        private static UserGroup ToModel(UserGroupEntity entity) => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Role = entity.Role,
            Status = entity.Status,
            Permissions = entity.Permissions
                .Select(grant => grant.Permission)
                .ToHashSet()
        };

        private static UserGroupListItem ToListItem(StoredGroup row) => new()
        {
            Id = row.Id,
            Name = row.Name,
            Role = row.Role,
            Status = row.Status,
            PermissionsCount = row.PermissionsCount,
            IsBase = BaseUserGroups.IsBase(row.Id)
        };

        /// <summary>The columns a page of the group list draws, before the shape it returns.</summary>
        private sealed class StoredGroup
        {
            public Guid Id { get; init; } = Guid.Empty;
            public string Name { get; init; } = string.Empty;
            public UserRole Role { get; init; }
            public UserStatus Status { get; init; }
            public int PermissionsCount { get; init; }
        }
    }
}
