using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Outcomes;
using Fluxy.Core.Subscriptions.ReadModels;

namespace Fluxy.Core.Subscriptions.Interfaces
{
    /// <summary>
    /// Sends MAG events to the devices of subscriptions, reads the journal and clears it on an
    /// administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The interface says nothing about HTTP, codes or cookies.</b> Which outcome becomes a 201
    /// is the transport layer's business; this contract answers the page, the staged row, or the
    /// map of fields that made the request unacceptable.
    /// </para>
    /// <para>
    /// <b>The send is one save.</b> Everything the rule checked and the row it stages commit
    /// together, so a request that is refused leaves no trace and a request that is accepted leaves
    /// exactly one row with <see cref="MagEventStatus.NotConfirmed"/>.
    /// </para>
    /// </remarks>
    public interface IMagEventService
    {
        /// <summary>Reads one filtered page of the journal, counted before paging.</summary>
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
        /// Applies the send rules, stages one row and commits it.
        /// </summary>
        /// <param name="magEvent">What the caller asked to send.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The identifier of the stored row, or the errors keyed by the C# name of the field that
        /// was refused. The refused fields are <c>SubscriptionId</c>, <c>Event</c> and
        /// <c>Message</c>; <c>MagDeviceMissing</c> is reported separately because it names the
        /// subscription rather than a body field.
        /// </returns>
        Task<MagEventSendResult> SendAsync(
            NewMagEvent magEvent,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes every row of the journal, whatever the reader had filtered the screen to.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>How many events were removed; zero when the table was already empty.</returns>
        Task<long> ClearAllAsync(CancellationToken cancellationToken = default);
    }
}
