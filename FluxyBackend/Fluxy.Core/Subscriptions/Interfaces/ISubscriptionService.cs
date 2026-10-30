using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Outcomes;
using Fluxy.Core.Subscriptions.ReadModels;

namespace Fluxy.Core.Subscriptions.Interfaces
{
    /// <summary>
    /// Reads the subscriptions and changes them on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately a service of its own rather than extra methods on
    /// <see cref="ITariffPlanService"/>: a plan is a price with a length and a delivery shape,
    /// while a subscription is one issued row that <i>points at</i> a plan and owns the parts the
    /// plan does not - the owner, the services, the whitelists and the bouquet list. The two
    /// disagree on nearly every rule, and one type carrying both would have to answer "whose
    /// price?" for every call.
    /// </para>
    /// <para>
    /// The interface says nothing about HTTP, codes or cookies. It reports
    /// <see cref="SubscriptionAction"/>, and which of those becomes a 201 and which a 409 is the
    /// transport layer's business.
    /// </para>
    /// </remarks>
    public interface ISubscriptionService
    {
        /// <summary>Reads one filtered page of the subscription table, counted before paging.</summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many subscriptions one page holds.</param>
        /// <param name="search">Substring of the playlist id, MAG MAC or Enigma2 MAC, or null.</param>
        /// <param name="ownerId">Owner to keep, or null for every owner.</param>
        /// <param name="creatorId">Issuer to keep, or null for every issuer.</param>
        /// <param name="status">Computed state to keep, or null for every state.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<SubscriptionPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? ownerId,
            Guid? creatorId,
            SubscriptionStatus? status,
            SubscriptionSortField sortBy,
            SubscriptionSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>Reads one subscription in full, for the edit form.</summary>
        /// <param name="id">Identifier of the subscription.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The subscription, or null when no row carries that identifier.</returns>
        Task<SubscriptionDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Issues a subscription.</summary>
        /// <param name="input">Everything the form supplied.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, carrying the identifier once the row exists.</returns>
        Task<SubscriptionOutcome> CreateAsync(
            NewSubscription input,
            CancellationToken cancellationToken = default);

        /// <summary>Changes a subscription. An absent field keeps the value the row holds.</summary>
        /// <param name="id">Identifier of the subscription to change.</param>
        /// <param name="patch">What to change about it.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, naming the field that was refused when one was.</returns>
        Task<SubscriptionOutcome> UpdateAsync(
            Guid id,
            SubscriptionPatch patch,
            CancellationToken cancellationToken = default);

        /// <summary>Deletes a subscription.</summary>
        /// <param name="id">Identifier of the subscription to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome: deleted, or not found.</returns>
        Task<SubscriptionOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extends the subscription's period from its end by its plan's duration.
        /// </summary>
        /// <param name="id">Identifier of the subscription to prolong.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// <see cref="SubscriptionAction.Prolonged"/>, a validation outcome naming the plan field
        /// when there is no plan to prolong by, or not found. The outcome carries no expiry: the
        /// caller re-reads the subscription to learn the new one, the same way it does after any
        /// other write.
        /// </returns>
        /// <remarks>
        /// The new expiry is <c>max(now, current expiry) + plan duration</c>: twenty days left plus
        /// one month is those twenty days and a month, and an expired subscription starts from now
        /// again. A row with no plan has no duration to add, which is why the refusal names the
        /// plan field rather than the expiry.
        /// </remarks>
        Task<SubscriptionOutcome> ProlongAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Mints a playlist id no subscription holds yet, without writing anything.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A fresh 32-hex identifier.</returns>
        /// <remarks>
        /// A generator rather than a field of the create form: the form has to show the
        /// identifier it is about to store, and the operator may press "regenerate" before
        /// saving. Nothing is written, and the loop that guarantees uniqueness is a probe against
        /// the table rather than a reservation.
        /// </remarks>
        Task<string> GeneratePlaylistIdAsync(CancellationToken cancellationToken = default);
    }
}
