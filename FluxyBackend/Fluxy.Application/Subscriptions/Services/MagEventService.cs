using Fluxy.Application.Subscriptions.Utils;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Interfaces;
using Fluxy.Core.Subscriptions.Outcomes;
using Fluxy.Core.Subscriptions.ReadModels;
using Fluxy.Core.Subscriptions.Repositories;

namespace Fluxy.Application.Subscriptions.Services
{
    /// <summary>
    /// Sends MAG events to the devices of subscriptions, reads the journal a page at a time, and
    /// clears it on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The send boundary is the point of this type.</b> The repository owns the SQL shape and
    /// the transport owns the bounds it refuses before a call; what is left is the three decisions
    /// this feature actually makes - what the operation's message means, which flags the row is
    /// stored with, and whether the subscription has a device to send to. The first two live in
    /// <see cref="MagEventRules"/>; the third needs a read and is decided here.
    /// </para>
    /// <para>
    /// <b>One save.</b> The row is staged once every rule passed, and the commit that follows is
    /// the only write of the operation - so a refused send leaves no trace and an accepted one
    /// leaves exactly one row, <see cref="MagEventStatus.NotConfirmed"/>.
    /// </para>
    /// </remarks>
    public sealed class MagEventService : IMagEventService
    {
        private readonly IMagEventRepository _events;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="MagEventService"/> class.
        /// </summary>
        /// <param name="events">Repository that stages the row and answers the reads.</param>
        /// <param name="unitOfWork">Unit of work the send commit belongs to.</param>
        public MagEventService(IMagEventRepository events, IUnitOfWork unitOfWork)
        {
            _events = events;
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public Task<MagEventPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            MagEventType? eventType,
            Guid? subscriptionId,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            MagEventSortField sortBy,
            MagEventSortOrder sortOrder,
            CancellationToken cancellationToken = default)
            => _events.GetPageAsync(
                page,
                pageSize,
                search,
                eventType,
                subscriptionId,
                dateFrom,
                dateTo,
                sortBy,
                sortOrder,
                cancellationToken);

        /// <inheritdoc />
        public async Task<MagEventSendResult> SendAsync(
            NewMagEvent magEvent,
            CancellationToken cancellationToken = default)
        {
            var write = MagEventRules.TryResolve(magEvent, out var errors);

            if (write is null)
            {
                return new MagEventSendResult { Errors = errors };
            }

            // The subscription has to exist before the device question makes sense: a body naming
            // an identifier no row carries is a broken field, not a subscription without a device.
            if (!await _events.SubscriptionExistsAsync(magEvent.SubscriptionId, cancellationToken))
            {
                errors["SubscriptionId"] =
                [
                    "There is no subscription with that identifier."
                ];

                return new MagEventSendResult { Errors = errors };
            }

            var magMac = await _events.GetMagMacAsync(magEvent.SubscriptionId, cancellationToken);

            if (MagEventRules.IsMagDeviceMissing(magMac))
            {
                // A fact about the row rather than a malformed body field, so it travels as its
                // own answer and the transport gives it its own code.
                return new MagEventSendResult { MagDeviceMissing = true };
            }

            var id = await _events.AddAsync(write, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new MagEventSendResult { EventId = id };
        }

        /// <inheritdoc />
        public Task<long> ClearAllAsync(CancellationToken cancellationToken = default)
            // No filter is passed because there is none to pass: the button says "clear the
            // journal", and a delete that obeyed the screen's filters would remove a different
            // set from the one the caller could see it naming. An already-empty table answers
            // zero rather than a refusal - the second press of a button that already did its job
            // is not an error.
            => _events.ClearAllAsync(cancellationToken);
    }
}
