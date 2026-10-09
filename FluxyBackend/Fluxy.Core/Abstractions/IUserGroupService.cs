using Fluxy.Core.Models.Users;

namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Reads every group of the installation and changes them on an admin's behalf.
    /// </summary>
    /// <remarks>
    /// Deliberately a separate service from <see cref="IUserAdminService"/> rather than a
    /// set of extra methods on it, and the difference is the whole interface: an account is a
    /// row with a password, a pending code, sessions and a guard, while a group is a name, a
    /// role, a state and a set of keys. The rules that guard an account's own state - a
    /// password, a mailed code, the anti-lock check - have no counterpart here, and a type
    /// carrying both would have to answer "whose password?" for every call.
    ///
    /// The interface says nothing about HTTP, codes or cookies. It reports
    /// <see cref="UserGroupAction"/>, and which of those becomes a 201 and which a 409 is the
    /// transport layer's business.
    ///
    /// The two refusals that protect the installation rather than the row - a base group may
    /// only be renamed, and a group with members may not be deleted - live here rather than
    /// in the controller, so that a second endpoint reaching the same action cannot forget
    /// them.
    /// </remarks>
    public interface IUserGroupService
    {
        /// <summary>
        /// Reads one page of every group, ordered, searched and filtered by the server.
        /// </summary>
        /// <param name="page">One-based page to answer.</param>
        /// <param name="pageSize">How many groups one page holds.</param>
        /// <param name="search">Case-insensitive substring matched against the name.</param>
        /// <param name="role">Role to keep, or null to keep every role.</param>
        /// <param name="status">State to keep, or null to keep every state.</param>
        /// <param name="field">Column to order by.</param>
        /// <param name="order">Which way to order it.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The page and the size of the filtered list.</returns>
        Task<UserGroupPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            UserRole? role,
            UserStatus? status,
            UserGroupSortField field,
            UserGroupSortOrder order,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads one group in full, for the edit form.
        /// </summary>
        /// <param name="id">Identifier of the group.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The group, or null when no group carries the identifier.</returns>
        Task<UserGroupDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Every group reduced to what a picker needs: identifier, name, role and state.
        /// </summary>
        /// <remarks>
        /// A deliberate second read shape rather than a page of the list, because a picker is
        /// not a table: it needs every group in one answer, and the list endpoint's page size
        /// ceiling would hand it a partial view with no way to know it was partial.
        /// </remarks>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Every group, ordered by name.</returns>
        Task<IReadOnlyList<UserGroupListItem>> GetAllAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a group.
        /// </summary>
        /// <param name="input">Name, role, state and permissions of the new group.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, carrying the identifier once the row exists.</returns>
        Task<UserGroupOutcome> CreateAsync(
            NewUserGroup input,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Changes a group. Every part of the patch that is absent is left alone.
        /// </summary>
        /// <param name="id">Identifier of the group to change.</param>
        /// <param name="patch">What to change about it.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, naming the field that was refused when one was.</returns>
        Task<UserGroupOutcome> UpdateAsync(
            Guid id,
            UserGroupPatch patch,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a group and every permission row it holds.
        /// </summary>
        /// <param name="id">Identifier of the group to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The outcome: removed, not found, refused because it is a base group, or refused
        /// because it still has members.
        /// </returns>
        Task<UserGroupOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
