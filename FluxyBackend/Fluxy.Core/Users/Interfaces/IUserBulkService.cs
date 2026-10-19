using Fluxy.Core.Users.Commands;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Outcomes;

namespace Fluxy.Core.Users.Interfaces
{
    /// <summary>
    /// Applies one operation to a set of accounts and reports how each one ended.
    /// </summary>
    /// <remarks>
    /// Deliberately a service of its own rather than another method on
    /// <see cref="IUserAdminService"/>. The single-account operations and the collective one
    /// answer different questions: one names a row and ends in one outcome, the other names a
    /// set a table assembled on screen and ends in one entry per row of that set. A client
    /// asking for the second must not have to know that the first exists, and a service that
    /// carries both makes every reader ask which of the two shapes a call returns.
    ///
    /// The interface says nothing about HTTP, codes or cookies. It reports
    /// <see cref="BulkUserAction"/> and <see cref="AdminUserAction"/>, and which of those
    /// becomes a 200 and which becomes a 400 is the transport layer's business.
    /// </remarks>
    public interface IUserBulkService
    {
        /// <summary>
        /// Applies one operation to every account the request names, and reports how each one
        /// ended.
        /// </summary>
        /// <param name="operation">Which operation, over which accounts, and where to move them.</param>
        /// <param name="actingUserId">The account making the request.</param>
        /// <param name="granted">The permissions the caller's group holds.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// One entry per named account - in the order the operation named them - each carrying
        /// the very <see cref="AdminUserAction"/> the corresponding single-account method
        /// would have returned.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>This is an orchestration, not a second implementation.</b> Every entry is
        /// produced by calling the single-account method of
        /// <see cref="IUserAdminService"/>, so the four rules that protect the caller (an
        /// account may not delete, block or demote itself) and the one that protects the
        /// target (the role must be inside the caller's grants) cannot be forgotten here:
        /// they are the methods, not something this method has to remember to do. The same is
        /// true of the side effects - a block still ends every session it holds, a move still
        /// checks the grant for the role being left and the one being arrived in - because
        /// they are inside those methods.
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
