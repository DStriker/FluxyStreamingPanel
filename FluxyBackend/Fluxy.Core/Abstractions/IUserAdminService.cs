using Fluxy.Core.Models.Users;

namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Reads every account of the installation and changes them on an admin's behalf.
    /// </summary>
    /// <remarks>
    /// Deliberately a different service from <see cref="IProfileService"/> rather than a set of
    /// extra methods on it, and the difference is the whole interface: a profile is what an
    /// account does to itself behind its own password, a managed account is what an operator
    /// does to somebody else behind an admin session. The two disagree on nearly every rule -
    /// no password is asked for, no code is mailed, no anti-lock check applies to a guard
    /// somebody else is setting - and one type carrying both would have to answer "whose
    /// password?" for every call.
    ///
    /// The interface says nothing about HTTP, codes or cookies. It reports
    /// <see cref="AdminUserAction"/>, and which of those becomes a 201 and which becomes a
    /// 409 is the transport layer's business.
    ///
    /// Where an operation can take away the caller's own access - deleting the account, the
    /// caller's own; blocking it; lowering its level - it is refused here rather than in the
    /// controller, so a future endpoint cannot reach the same action by forgetting a check.
    /// </remarks>
    public interface IUserAdminService
    {
        /// <summary>
        /// Reads one page of every account, ordered, searched and filtered by the server.
        /// </summary>
        /// <param name="page">One-based page to answer.</param>
        /// <param name="pageSize">How many accounts one page holds.</param>
        /// <param name="search">
        /// Case-insensitive substring matched against the username or the email address, or
        /// null when the whole list is wanted.
        /// </param>
        /// <param name="role">Access level to keep, or null to keep every level.</param>
        /// <param name="status">State to keep, or null to keep every state.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run it.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The page and the total the filter admits. The bounds are not re-checked here -
        /// they are a rule about the request, and the request has already been answered for
        /// breaking them before a query is built.
        /// </returns>
        Task<AdminUserPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            UserRole? role,
            UserStatus? status,
            AdminUserSortField sortBy,
            AdminUserSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads one account in full, for the edit form.
        /// </summary>
        /// <param name="userId">Identifier of the account.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The account, or null when there is no such account.</returns>
        Task<AdminUserDetail?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates an account from what the form sent.
        /// </summary>
        /// <param name="user">The account to create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// <see cref="AdminUserAction.Created"/> with the new identifier,
        /// <see cref="AdminUserAction.InvalidInput"/> with the offending fields,
        /// <see cref="AdminUserAction.AlreadyExists"/> when another account holds the username
        /// or the address.
        /// </returns>
        Task<AdminUserOutcome> CreateAsync(NewUser user, CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies the fields an edit body names to one account.
        /// </summary>
        /// <param name="userId">Identifier of the account to change.</param>
        /// <param name="patch">The fields to change. Absent fields are left alone.</param>
        /// <param name="actingUserId">Account the request arrived with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// <see cref="AdminUserAction.Updated"/>, <see cref="AdminUserAction.NotFound"/>, one
        /// of the <c>Cannot</c> refusals when the change would take the caller's own access
        /// away, or a validation outcome.
        /// </returns>
        /// <remarks>
        /// Two side effects follow a change and are part of it rather than separate steps: a
        /// status of Blocked revokes every session the account holds, and a new password
        /// revokes them too - whoever held the old password may hold a session with it, and
        /// the honest reading of that is to assume they do.
        /// </remarks>
        Task<AdminUserOutcome> UpdateAsync(
            Guid userId,
            UserPatch patch,
            Guid actingUserId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Moves an account out of Unregistered, stamping the moment it happened.
        /// </summary>
        /// <param name="userId">Identifier of the account.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// <see cref="AdminUserAction.RegistrationConfirmed"/>,
        /// <see cref="AdminUserAction.InvalidStatus"/> when the account is not waiting for a
        /// confirmation, or <see cref="AdminUserAction.NotFound"/>.
        /// </returns>
        Task<AdminUserOutcome> ConfirmRegistrationAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Puts an account into Blocked, or takes it out again.
        /// </summary>
        /// <param name="userId">Identifier of the account.</param>
        /// <param name="blocked">True to block, false to unblock.</param>
        /// <param name="actingUserId">Account the request arrived with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// <see cref="AdminUserAction.Blocked"/> or <see cref="AdminUserAction.Unblocked"/>.
        /// Blocking is idempotent - an account already blocked answers the same way, because
        /// the answer does not depend on where it started - while unblocking refuses an account
        /// that is not blocked, because where it goes back to does.
        /// </returns>
        /// <remarks>
        /// Blocking ends every session the account holds: a blocked account is refused at
        /// sign-in, so a session that outlives the block would keep working until its refresh
        /// token expired for no reason anybody could defend.
        ///
        /// Unblock is not the inverse of setting the status to Registered - it is the inverse
        /// of the block itself. An account that was never confirmed goes back to Unregistered,
        /// an account with a registration stamp goes back to Registered, and no state is
        /// invented either way.
        /// </remarks>
        Task<AdminUserOutcome> SetBlockedAsync(
            Guid userId,
            bool blocked,
            Guid actingUserId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an account and everything that references it.
        /// </summary>
        /// <param name="userId">Identifier of the account to delete.</param>
        /// <param name="actingUserId">Account the request arrived with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// <see cref="AdminUserAction.Removed"/>, <see cref="AdminUserAction.NotFound"/>, or
        /// <see cref="AdminUserAction.CannotDeleteSelf"/>.
        /// </returns>
        /// <remarks>
        /// The refresh tokens, the pending changes and the login guard rules go with the row
        /// through foreign keys that are <c>ON DELETE CASCADE</c>: a session or a code that
        /// outlives its account is a credential for nobody. The deletion is a delete rather
        /// than a block because the operator asked for the row to stop existing, and an id
        /// that answers 404 afterwards is the answer to that.
        /// </remarks>
        Task<AdminUserOutcome> DeleteAsync(
            Guid userId,
            Guid actingUserId,
            CancellationToken cancellationToken = default);
    }
}
