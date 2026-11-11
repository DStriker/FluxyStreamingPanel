using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.ReadModels;

namespace Fluxy.Core.Subscriptions.Repositories
{
    /// <summary>
    /// Reads the MAG event journal, stages new rows, and wipes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The repository owns the query shape.</b> The predicate every filter contributes, the
    /// count taken under that same predicate, the order with its tie-break, the page cut and the
    /// join that resolves the device address all live in one place, so <c>total</c> can never
    /// describe a different set from the page it sits beside.
    /// </para>
    /// <para>
    /// <b>Writes are staged, never saved.</b> <see cref="AddAsync"/> hands the row to the tracker;
    /// the service owns the single save, so the caller that sends an event decides the transaction
    /// boundary.
    /// </para>
    /// </remarks>
    public interface IMagEventRepository
    {
        /// <summary>
        /// Reads one filtered page of the journal, counted before paging.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many events one page holds.</param>
        /// <param name="search">Case-insensitive substring of the message or the device MAC, or null.</param>
        /// <param name="eventType">Operation to keep, or null for every operation.</param>
        /// <param name="subscriptionId">Subscription to keep, or null for every subscription.</param>
        /// <param name="dateFrom">Earliest <c>created_at</c> to keep, inclusive, or null.</param>
        /// <param name="dateTo">Latest <c>created_at</c> to keep, inclusive, or null.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<MagEventPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            MagEventType? eventType,
            Guid? subscriptionId,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            MagEventSortField sortBy,
            MagEventSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages one event row, left for the caller's single save.
        /// </summary>
        /// <param name="magEvent">Resolved values the row is written with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Identifier the staged row carries.</returns>
        Task<Guid> AddAsync(MagEventWrite magEvent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes every row of the journal and reports how many were removed.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// <para>
        /// <b>The whole table, whatever the screen was filtered to.</b> The button says "clear the
        /// journal", and a delete that obeyed a filter the caller happened not to send would wipe a
        /// different set from the one it named.
        /// </para>
        /// <para>
        /// <b><c>ExecuteDelete</c> is immediate and outside the unit of work</b>, so there is no
        /// save for the service to call and no transaction to roll back: the statement either ran
        /// or it did not. An empty table is success with a count of zero, not a refusal - the
        /// second press of a button that already did its job must not read as an error.
        /// </para>
        /// </remarks>
        /// <returns>How many rows were deleted; zero when the table was already empty.</returns>
        Task<long> ClearAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the stored MAG address of one subscription.
        /// </summary>
        /// <param name="subscriptionId">Identifier of the subscription.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The address, or null when the subscription exists and has no MAC, or when no
        /// subscription carries the identifier.
        /// </returns>
        Task<string?> GetMagMacAsync(Guid subscriptionId, CancellationToken cancellationToken = default);

        /// <summary>Whether a subscription row carries the identifier.</summary>
        /// <param name="subscriptionId">Identifier to probe.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> SubscriptionExistsAsync(
            Guid subscriptionId,
            CancellationToken cancellationToken = default);
    }
}
