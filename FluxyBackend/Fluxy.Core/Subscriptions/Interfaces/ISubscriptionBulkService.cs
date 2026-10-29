using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Outcomes;
using Fluxy.Core.Users.Enums;

namespace Fluxy.Core.Subscriptions.Interfaces
{
    /// <summary>
    /// Applies one operation to a set of subscriptions, answering for each row.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Its own contract for the reason <c>IUserBulkService</c> gives: a bulk request names a set
    /// a table assembled on screen and answers with an entry per row, which is a different shape
    /// from the one outcome per row of the single-subscription methods - a client asking for the
    /// second must not have to know which single-row method it is built on.
    /// </para>
    /// <para>
    /// The operations themselves are those methods in a loop. What is added here is collective
    /// only: the plan change that refuses a row whose last service would be cleared, and the
    /// block that reuses the account block and refuses the caller's own account.
    /// </para>
    /// </remarks>
    public interface ISubscriptionBulkService
    {
        /// <summary>Runs one operation over the named subscriptions and reports each row.</summary>
        /// <param name="operation">Which operation, over which rows, and the value it writes.</param>
        /// <param name="actingUserId">Account the request arrived with, for the block's self-refusal.</param>
        /// <param name="granted">
        /// Permissions the caller's group holds, for the account block's per-target reach: the
        /// subscription grant authorises reaching the operation, and the account grant decides
        /// whose account it may touch.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<BulkSubscriptionOutcome> BulkAsync(
            BulkSubscriptionOperation operation,
            Guid actingUserId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default);
    }
}
