using Fluxy.Core.Abstractions.Repositories;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IUserGroupRepository"/> for the startup
    /// reconciliation of the three base groups.
    /// </summary>
    /// <remarks>
    /// The methods stage changes and leave the save to <see cref="IUnitOfWork"/>, so the seeder
    /// writes a repaired group and its grants in one transaction. A permission that is already
    /// granted is not added twice here; the seeder diffs the catalog against
    /// <see cref="GetPermissionsAsync"/> first, and the unique index is the last line.
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
        public Task<UserRole?> GetRoleAsync(
            Guid groupId,
            CancellationToken cancellationToken = default)
            => _context.UserGroups
                .AsNoTracking()
                .Where(group => group.Id == groupId)
                .Select(group => (UserRole?)group.Role)
                .FirstOrDefaultAsync(cancellationToken);

        /// <inheritdoc />
        public Task AddAsync(
            NewUserGroup group,
            CancellationToken cancellationToken = default)
        {
            // The identifier is optional on the model because the three base groups have fixed
            // ones; every other caller leaves it out and gets a generated one.
            _context.UserGroups.Add(new UserGroupEntity(group.Id ?? Guid.NewGuid())
            {
                Name = group.Name,
                Role = group.Role,
                Status = group.Status
            });

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task SetRoleAsync(
            Guid groupId,
            UserRole role,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.UserGroups.FindAsync([groupId], cancellationToken)
                ?? throw new InvalidOperationException(
                    $"User group '{groupId}' was not found when its role was being reconciled.");

            entity.Role = role;
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
    }
}
