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
    ///
    /// <b>The same is true of the per-target permission.</b> Every method takes the permissions
    /// the caller's group holds and refuses an account whose role is not one of the roles those
    /// permissions reach, because the role of the target is a fact about the row and this
    /// service is what has the row in hand. Refusing it in the controller would make the rule
    /// depend on the next endpoint remembering to apply it - and the endpoints share one
    /// controller today only by accident.
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
        /// <param name="groupId">Group to keep, or null to keep every group.</param>
        /// <param name="status">State to keep, or null to keep every state.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run it.</param>
        /// <param name="granted">Permissions the caller's group holds.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The page and the total the filter admits. The bounds are not re-checked here -
        /// they are a rule about the request, and the request has already been answered for
        /// breaking them before a query is built.
        /// </returns>
        /// <remarks>
        /// Only the accounts of the roles <paramref name="granted"/> may view are counted and
        /// returned, and the filter runs before the count because the pager shows it: a total
        /// taken over every row would advertise pages whose rows the caller was never going to
        /// be shown.
        /// </remarks>
        Task<AdminUserPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? groupId,
            UserStatus? status,
            AdminUserSortField sortBy,
            AdminUserSortOrder sortOrder,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads one account in full, for the edit form.
        /// </summary>
        /// <param name="userId">Identifier of the account.</param>
        /// <param name="granted">Permissions the caller's group holds.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The account; <see cref="AdminUserAction.NotFound"/> when there is no such account;
        /// <see cref="AdminUserAction.PermissionDenied"/> when there is one whose role
        /// <paramref name="granted"/> may not read.
        /// </returns>
        Task<AdminUserReadResult> GetAsync(
            Guid userId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates an account from what the form sent.
        /// </summary>
        /// <param name="user">The account to create.</param>
        /// <param name="granted">Permissions the caller's group holds.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// <see cref="AdminUserAction.Created"/> with the new identifier,
        /// <see cref="AdminUserAction.InvalidInput"/> with the offending fields,
        /// <see cref="AdminUserAction.AlreadyExists"/> when another account holds the username
        /// or the address, or <see cref="AdminUserAction.PermissionDenied"/> when the group
        /// named is a group of a role the caller may not write accounts of.
        /// </returns>
        /// <remarks>
        /// The permission is checked as soon as the target group has been read and before
        /// anything else is looked up, so an operator without the grant cannot learn whether a
        /// username is already taken.
        /// </remarks>
        Task<AdminUserOutcome> CreateAsync(
            NewUser user,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies the fields an edit body names to one account.
        /// </summary>
        /// <param name="userId">Identifier of the account to change.</param>
        /// <param name="patch">The fields to change. Absent fields are left alone.</param>
        /// <param name="actingUserId">Account the request arrived with.</param>
        /// <param name="granted">Permissions the caller's group holds.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// <see cref="AdminUserAction.Updated"/>, <see cref="AdminUserAction.NotFound"/>, one
        /// of the <c>Cannot</c> refusals when the change would take the caller's own access
        /// away, <see cref="AdminUserAction.PermissionDenied"/> when the account's role is not
        /// one the caller may change, or a validation outcome.
        /// </returns>
        /// <remarks>
        /// Two side effects follow a change and are part of it rather than separate steps: a
        /// status of Blocked revokes every session the account holds, and a new password
        /// revokes them too - whoever held the old password may hold a session with it, and
        /// the honest reading of that is to assume they do.
        ///
        /// A change that moves the account into another group needs the grant for the role it
        /// is leaving <i>and</i> for the role it is arriving in. Neither alone is enough: the
        /// first is what makes the row editable at all, and the second is what stops a caller
        /// who may run the clients from promoting one of them into a group of administrators.
        /// </remarks>
        Task<AdminUserOutcome> UpdateAsync(
            Guid userId,
            UserPatch patch,
            Guid actingUserId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Moves an account out of Unregistered, stamping the moment it happened.
        /// </summary>
        /// <param name="userId">Identifier of the account.</param>
        /// <param name="granted">Permissions the caller's group holds.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// <see cref="AdminUserAction.RegistrationConfirmed"/>,
        /// <see cref="AdminUserAction.InvalidStatus"/> when the account is not waiting for a
        /// confirmation, <see cref="AdminUserAction.NotFound"/>, or
        /// <see cref="AdminUserAction.PermissionDenied"/>.
        /// </returns>
        Task<AdminUserOutcome> ConfirmRegistrationAsync(
            Guid userId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Puts an account into Blocked, or takes it out again.
        /// </summary>
        /// <param name="userId">Identifier of the account.</param>
        /// <param name="blocked">True to block, false to unblock.</param>
        /// <param name="actingUserId">Account the request arrived with.</param>
        /// <param name="granted">Permissions the caller's group holds.</param>
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
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an account and everything that references it.
        /// </summary>
        /// <param name="userId">Identifier of the account to delete.</param>
        /// <param name="actingUserId">Account the request arrived with.</param>
        /// <param name="granted">Permissions the caller's group holds.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// <see cref="AdminUserAction.Removed"/>, <see cref="AdminUserAction.NotFound"/>,
        /// <see cref="AdminUserAction.CannotDeleteSelf"/>, or
        /// <see cref="AdminUserAction.PermissionDenied"/>.
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
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies one operation to every account the request names, reporting how each one
        /// ended.
        /// </summary>
        /// <param name="operation">Which operation, over which accounts, and where to move them.</param>
        /// <param name="actingUserId">Account the request arrived with.</param>
        /// <param name="granted">Permissions the caller's group holds.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// One <see cref="BulkUserItemResult"/> per identifier named, in the order it was
        /// named - each carrying the very <see cref="AdminUserAction"/> the corresponding
        /// single-account method would have returned.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>This is an orchestration, not a second implementation.</b> Every entry is
        /// produced by calling the single-account method above, so the four rules that protect
        /// the caller (an account may not delete, block or demote itself) and the one that
        /// protects the target (the role must be inside the caller's grants) cannot be
        /// forgotten here: they are the methods, not something this method has to remember to
        /// do. The same is true of the side effects - a block still ends every session it
        /// holds, a move still checks the grant for the role being left and the one being
        /// arrived in - because they are inside those methods.
        /// </para>
        /// <para>
        /// <b>Partial success is the answer, and not by accident.</b> The rows of a page are
        /// independent and one of them being the caller's own is a fact about that row alone,
        /// so an entry that was refused does not undo the nineteen that were not. An
        /// all-or-nothing shape would answer "delete these 20" with a change to nothing at all
        /// because one row was protected, and would then have to explain which - by throwing
        /// away the refusal that said so.
        /// </para>
        /// <para>
        /// Each entry costs its own read and its own write, which is the deliberate price of
        /// the paragraph above. One <c>ExecuteUpdate</c> over a hundred rows would be one
        /// statement instead of two hundred, and would also be a second implementation of
        /// every rule in those rows. The request is bounded at one page of accounts, so the
        /// round trips are bounded with it.
        /// </para>
        /// </remarks>
        Task<BulkUserOutcome> BulkAsync(
            BulkUserOperation operation,
            Guid actingUserId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default);
    }
}
