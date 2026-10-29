using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Models;
using Fluxy.Core.Subscriptions.ReadModels;
using Fluxy.Core.Subscriptions.Rows;

namespace Fluxy.Core.Subscriptions.Repositories
{
    /// <summary>
    /// Reads and writes the subscriptions and their bouquet junction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The repository owns the query shape - the page with its three filters and two orders, the
    /// detail, the uniqueness probes and the replacement of the bouquet set - while the service
    /// owns the rules that decide whether a request may reach a write at all.
    /// </para>
    /// <para>
    /// The computed status is <b>projected here</b> rather than stored, and the status filter is
    /// translated into the same predicate before the count: a tag drawn from one rule and a
    /// filter built from another would be two answers to one question, and the day they
    /// disagreed the table would show rows the pager did not count.
    /// </para>
    /// </remarks>
    public interface ISubscriptionRepository
    {
        /// <summary>Reads one filtered page of the table, counted before paging.</summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many subscriptions one page holds.</param>
        /// <param name="search">Substring of the playlist id or either MAC, or null.</param>
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

        /// <summary>Reads one subscription in full, with its bouquet list and plan-derived values.</summary>
        /// <param name="id">Identifier of the subscription.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<SubscriptionDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads one subscription for a change, with its bouquet links loaded and the row tracked.
        /// </summary>
        /// <param name="id">Identifier of the subscription.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<Subscription?> FindForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        /// <summary>Whether another subscription already holds the playlist id.</summary>
        /// <param name="playlistId">Identifier to probe, compared as stored.</param>
        /// <param name="exceptSubscription">Subscription to compare against itself, or null for a create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> IsPlaylistIdTakenAsync(
            string playlistId,
            Guid? exceptSubscription,
            CancellationToken cancellationToken = default);

        /// <summary>Whether a subscription already holds the playlist id at all.</summary>
        /// <param name="playlistId">Identifier to probe.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// The generator's own probe, and separate from
        /// <see cref="IsPlaylistIdTakenAsync"/> only by the exclusion it does not need: a minted
        /// identifier belongs to no row yet, so there is no row to compare against itself.
        /// </remarks>
        Task<bool> PlaylistIdExistsAsync(
            string playlistId,
            CancellationToken cancellationToken = default);

        /// <summary>Whether another subscription already holds the MAG MAC.</summary>
        /// <param name="mac">Canonical <c>XX:XX:XX:XX:XX:XX</c>.</param>
        /// <param name="exceptSubscription">Subscription to compare against itself, or null for a create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> IsMagMacTakenAsync(
            string mac,
            Guid? exceptSubscription,
            CancellationToken cancellationToken = default);

        /// <summary>Whether another subscription already holds the Enigma2 MAC.</summary>
        /// <param name="mac">Canonical <c>XX:XX:XX:XX:XX:XX</c>.</param>
        /// <param name="exceptSubscription">Subscription to compare against itself, or null for a create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> IsEnigma2MacTakenAsync(
            string mac,
            Guid? exceptSubscription,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads an account as a subscription save needs it, or null when there is no such account.
        /// </summary>
        /// <param name="userId">Identifier of the account.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<SubscriptionOwnerContext?> GetOwnerContextAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads a tariff plan as a subscription save needs it, or null when there is no such plan.
        /// </summary>
        /// <param name="planId">Identifier of the plan.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<SubscriptionPlanContext?> GetPlanContextAsync(
            Guid planId,
            CancellationToken cancellationToken = default);

        /// <summary>Every bouquet the installation has, in the bouquets' own order.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// The default a create without a plan starts from: "all bouquets are preselected" is
        /// only expressible against the set that exists at that moment.
        /// </remarks>
        Task<IReadOnlyList<Guid>> GetAllBouquetIdsAsync(
            CancellationToken cancellationToken = default);

        /// <summary>Reads which of the named bouquets exist.</summary>
        /// <param name="bouquetIds">Identifiers to test.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The subset of <paramref name="bouquetIds"/> that names a bouquet.</returns>
        Task<IReadOnlySet<Guid>> GetExistingBouquetIdsAsync(
            IReadOnlyCollection<Guid> bouquetIds,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a new subscription and its bouquet set.</summary>
        /// <param name="subscription">Values the row is written with.</param>
        /// <param name="bouquetIds">Bouquets it carries while no plan is attached.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Identifier the new row carries.</returns>
        Task<Guid> AddAsync(
            NewSubscription subscription,
            IReadOnlyList<Guid> bouquetIds,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a change to the stored fields of a subscription.</summary>
        /// <param name="id">Identifier of the subscription.</param>
        /// <param name="subscription">Values the row must carry after the change.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>False when no row carries the identifier.</returns>
        Task<bool> UpdateAsync(
            Guid id,
            Subscription subscription,
            CancellationToken cancellationToken = default);

        /// <summary>Stages the removal of a subscription.</summary>
        /// <param name="id">Identifier of the subscription.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>False when no row carries the identifier.</returns>
        Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Stages the replacement of the subscription's own bouquet set.</summary>
        /// <param name="subscriptionId">Identifier of the subscription.</param>
        /// <param name="bouquetIds">Every bouquet it must carry, empty to carry none.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task ReplaceBouquetsAsync(
            Guid subscriptionId,
            IReadOnlyList<Guid> bouquetIds,
            CancellationToken cancellationToken = default);

        /// <summary>Counts the subscriptions attached to a tariff plan.</summary>
        /// <param name="planId">Identifier of the plan.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// The probe behind the plan's own delete refusal: the foreign key is <c>RESTRICT</c>, so
        /// the database would refuse it anyway, and asking first answers with a code and a count
        /// instead of a provider error.
        /// </remarks>
        Task<int> CountForPlanAsync(Guid planId, CancellationToken cancellationToken = default);
    }
}
