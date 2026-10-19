using Fluxy.Core.Models.Users;

namespace Fluxy.Core.Abstractions.Repositories
{
    /// <summary>
    /// Reads and writes the groups accounts belong to.
    /// </summary>
    /// <remarks>
    /// The repository owns the query shape of the group list and the storage of a group's
    /// permission rows. The service owns the rules that protect the installation - a base group
    /// may only be renamed and a group with members may not be deleted - and the validation of
    /// a request.
    /// </remarks>
    public interface IUserGroupRepository
    {
        /// <summary>
        /// Reads one filtered page of the groups, counted before paging.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many groups one page holds.</param>
        /// <param name="search">Case-insensitive substring of the name, or null.</param>
        /// <param name="role">Role to keep, or null for every role.</param>
        /// <param name="status">State to keep, or null for every state.</param>
        /// <param name="field">Column to order by.</param>
        /// <param name="order">Which way to order it.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<UserGroupPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            UserRole? role,
            UserStatus? status,
            UserGroupSortField field,
            UserGroupSortOrder order,
            CancellationToken cancellationToken = default);

        /// <summary>Reads one group in full, including its permission set and member count.</summary>
        /// <param name="id">Identifier of the group.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<UserGroupDetail?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        /// <summary>Reads every group reduced to what a picker needs, ordered by name.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<IReadOnlyList<UserGroupListItem>> GetAllAsync(
            CancellationToken cancellationToken = default);

        /// <summary>Reads the role and state of one group, or null when it does not exist.</summary>
        /// <param name="id">Identifier of the group.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<UserGroupSummary?> GetSummaryAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds one group for a change, with its permission rows loaded and the row tracked.
        /// </summary>
        /// <param name="id">Identifier of the group.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<UserGroup?> FindForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the level a group holds, or null when no row carries the identifier.
        /// </summary>
        /// <param name="groupId">Identifier of the group.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<UserRole?> GetRoleAsync(
            Guid groupId,
            CancellationToken cancellationToken = default);

        /// <summary>Whether another group already holds the name.</summary>
        /// <param name="name">Name to probe, compared case sensitively.</param>
        /// <param name="exceptGroup">Group to compare against itself, or null for a create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> IsNameTakenAsync(
            string name,
            Guid? exceptGroup,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a new group row for the next save.</summary>
        /// <param name="group">Values the row is written with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Identifier the new row carries.</returns>
        Task<Guid> AddAsync(
            NewUserGroup group,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a new group together with exactly the permissions it must grant.
        /// </summary>
        /// <param name="group">Values the row is written with.</param>
        /// <param name="permissions">Every permission the new group must grant.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Identifier the new row carries.</returns>
        Task<Guid> AddWithPermissionsAsync(
            NewUserGroup group,
            IReadOnlySet<UserPermission> permissions,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a new name on a group read for update.</summary>
        /// <param name="groupId">Identifier of the group.</param>
        /// <param name="name">Name the row must carry.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetNameAsync(
            Guid groupId,
            string name,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a change of the level a group holds.</summary>
        /// <param name="groupId">Identifier of the group.</param>
        /// <param name="role">Level the group must hold.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetRoleAsync(
            Guid groupId,
            UserRole role,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a change of the state a group holds.</summary>
        /// <param name="groupId">Identifier of the group.</param>
        /// <param name="status">State the group must hold.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetStatusAsync(
            Guid groupId,
            UserStatus status,
            CancellationToken cancellationToken = default);

        /// <summary>Reads every permission a group grants, in no particular order.</summary>
        /// <param name="groupId">Identifier of the group.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<IReadOnlyList<UserPermission>> GetPermissionsAsync(
            Guid groupId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages the group's permission set to be exactly the one named.
        /// </summary>
        /// <param name="groupId">Identifier of the group read for update.</param>
        /// <param name="permissions">Every permission the group must grant.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task ReplacePermissionsAsync(
            Guid groupId,
            IReadOnlySet<UserPermission> permissions,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a permission the group must grant.</summary>
        /// <param name="groupId">Identifier of the group.</param>
        /// <param name="permission">Permission to add.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task AddPermissionAsync(
            Guid groupId,
            UserPermission permission,
            CancellationToken cancellationToken = default);

        /// <summary>Stages the removal of a permission the group must no longer grant.</summary>
        /// <param name="groupId">Identifier of the group.</param>
        /// <param name="permission">Permission to remove.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task RemovePermissionAsync(
            Guid groupId,
            UserPermission permission,
            CancellationToken cancellationToken = default);

        /// <summary>Counts the accounts in a group.</summary>
        /// <param name="groupId">Identifier of the group.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<int> CountMembersAsync(
            Guid groupId,
            CancellationToken cancellationToken = default);

        /// <summary>Stages the removal of a group read for update, with its permission rows.</summary>
        /// <param name="groupId">Identifier of the group.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task RemoveAsync(
            Guid groupId,
            CancellationToken cancellationToken = default);
    }
}
