using Fluxy.Core.Models.Users;

namespace Fluxy.Core.Abstractions.Repositories
{
    /// <summary>
    /// Reads and writes the groups accounts belong to, for the operations the startup seeder
    /// needs.
    /// </summary>
    /// <remarks>
    /// This is deliberately not the whole group resource: listing, renaming, member counts and
    /// the refusal rules of the admin endpoints arrive with the service that owns them. What is
    /// here is the reconciliation of the three base groups, which runs before any request can be
    /// served and therefore cannot wait for that service.
    /// </remarks>
    public interface IUserGroupRepository
    {
        /// <summary>
        /// Reads the level a group holds, or null when no row carries the identifier.
        /// </summary>
        /// <param name="groupId">Identifier of the group.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<UserRole?> GetRoleAsync(
            Guid groupId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a new group row for the next save.
        /// </summary>
        /// <param name="group">Values the row is written with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task AddAsync(
            NewUserGroup group,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a change of the level a group holds.
        /// </summary>
        /// <param name="groupId">Identifier of the group.</param>
        /// <param name="role">Level the group must hold.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetRoleAsync(
            Guid groupId,
            UserRole role,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads every permission a group grants, in no particular order.
        /// </summary>
        /// <param name="groupId">Identifier of the group.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<IReadOnlyList<UserPermission>> GetPermissionsAsync(
            Guid groupId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a permission the group must grant.
        /// </summary>
        /// <param name="groupId">Identifier of the group.</param>
        /// <param name="permission">Permission to add.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task AddPermissionAsync(
            Guid groupId,
            UserPermission permission,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages the removal of a permission the group must no longer grant.
        /// </summary>
        /// <param name="groupId">Identifier of the group.</param>
        /// <param name="permission">Permission to remove.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task RemovePermissionAsync(
            Guid groupId,
            UserPermission permission,
            CancellationToken cancellationToken = default);
    }
}
