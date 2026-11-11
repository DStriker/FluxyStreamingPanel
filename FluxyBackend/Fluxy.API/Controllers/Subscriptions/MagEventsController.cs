using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Subscriptions;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Interfaces;
using Fluxy.Core.Subscriptions.ReadModels;
using Fluxy.Core.Subscriptions.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Subscriptions
{
    /// <summary>
    /// The journal of events sent to MAG devices: which subscription's device, which operation, and
    /// whether it was confirmed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A send is a create, and the collection is where it happens.</b> <c>POST
    /// /admin/mag-events</c> records one event for one subscription's device and answers 201 with a
    /// <c>Location</c> naming the new row. The GET beside it reads the journal; the DELETE clears
    /// it. There is no <c>PATCH</c> or <c>DELETE {id}</c>: an event is a fact that was sent, and
    /// editing or removing one would be editing the record of what happened.
    /// </para>
    /// <para>
    /// <b>The two endpoints reuse the subscription grants.</b> Reading the journal is reading the
    /// subscription surface (<c>viewSubscriptions</c>); sending an event and wiping the journal are
    /// changes to what that surface holds (<c>editSubscriptions</c>), and both carry a fresh
    /// antiforgery pair. No third permission was invented: the owner ratified the reuse, so this
    /// feature changes no catalog, policy, locale or test that was not already about subscriptions.
    /// </para>
    /// <para>
    /// <b>Every rule about a value lives in Core or in <c>MagEventRules</c>.</b> The query contract
    /// is <see cref="MagEventQuery"/>, tested without a running host; the send contract is
    /// <c>MagEventRules</c> in the application layer, tested the same way. This controller keeps the
    /// HTTP half - the status codes, the antiforgery order, and the body.
    /// </para>
    /// <para>
    /// <b>The clear ignores the screen's filters on purpose.</b> The button says "clear the
    /// journal", so the whole table goes whatever the reader had narrowed it to; a delete that
    /// obeyed a filter the caller happened not to send would wipe a different set from the one it
    /// named.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/mag-events")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class MagEventsController : ApiControllerBase
    {
        private readonly IMagEventService _events;

        /// <summary>
        /// Initializes a new instance of the <see cref="MagEventsController"/> class.
        /// </summary>
        /// <param name="events">Service that sends, reads and clears the journal.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public MagEventsController(
            IMagEventService events,
            IAntiforgery antiforgery,
            ILogger<MagEventsController> logger)
            : base(antiforgery, logger)
        {
            _events = events;
        }

        /// <summary>
        /// Reports one page of the journal, filtered and ordered the way the query asked.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many events one page holds.</param>
        /// <param name="search">Substring of the message or the device MAC, or null.</param>
        /// <param name="eventType">Operation to keep, as text, or null.</param>
        /// <param name="subscriptionId">Subscription to keep, as a uuid, or null.</param>
        /// <param name="dateFrom">Earliest creation instant to keep, inclusive, or null.</param>
        /// <param name="dateTo">Latest creation instant to keep, inclusive, or null.</param>
        /// <param name="sortBy">Column to order by; <c>createdAt</c> when absent.</param>
        /// <param name="sortOrder">Direction; <c>desc</c> when absent.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with one page, or 400 naming the parameters that were broken.</returns>
        /// <remarks>
        /// <para>
        /// <b>Every parameter is parsed as plain text, case-insensitively.</b> Enum binding takes a
        /// member name spelled in full, so <c>event=playChannel</c> would be refused while
        /// <c>Event=PlayChannel</c> was accepted - an API that answers a conventional request this
        /// way teaches its callers to spell things oddly. The mapping lives in
        /// <see cref="MagEventQuery"/>, in Core.
        /// </para>
        /// <para>
        /// <b>An unknown <c>event</c> is refused, never treated as "no filter".</b> A mistyped
        /// operation silently widening the list to every operation would answer a request nobody
        /// made.
        /// </para>
        /// <para>
        /// <b>Both date bounds are independent and inclusive.</b> A later <c>dateFrom</c> than
        /// <c>dateTo</c> is a valid query describing an empty result: 200 with an empty page and a
        /// total of zero. <c>total</c> is counted under the same predicate as the page, so a
        /// filtered list's pages sum to its <c>total</c>.
        /// </para>
        /// <para>
        /// <b>The order is <c>createdAt</c> with an <c>id</c> tie-break in the same direction, and
        /// it is the repository's.</b> Two events can be recorded in the same instant; without the
        /// identifier the database is free to answer a different order on every read.
        /// </para>
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewSubscriptionsPolicy)]
        [ProducesResponseType<AdminMagEventPageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMagEvents(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null,
            [FromQuery] string? search = null,
            [FromQuery(Name = "event")] string? eventType = null,
            [FromQuery] string? subscriptionId = null,
            [FromQuery] string? dateFrom = null,
            [FromQuery] string? dateTo = null,
            [FromQuery] string? sortBy = "createdAt",
            [FromQuery] string? sortOrder = "desc",
            CancellationToken cancellationToken = default)
        {
            NoStore();

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            MergeListErrors(
                errors,
                ListQueryErrors.Common(
                    page ?? 1,
                    pageSize ?? MagEventListLimits.DefaultPageSize,
                    search,
                    MagEventListLimits.MaxPageSize,
                    MagEventListLimits.MaxSearchLength));

            var query = MagEventQuery.TryParse(
                page,
                pageSize,
                search,
                eventType,
                subscriptionId,
                dateFrom,
                dateTo,
                sortBy,
                sortOrder,
                out var queryErrors);

            MergeListErrors(errors, queryErrors);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var journal = await _events.GetPageAsync(
                query!.Page,
                query.PageSize,
                query.Search,
                query.EventType,
                query.SubscriptionId,
                query.DateFrom,
                query.DateTo,
                query.SortBy,
                query.SortOrder,
                cancellationToken);

            return Ok(new AdminMagEventPageResponse
            {
                Items = journal.Items.Select(ToResponse).ToList(),
                Total = journal.Total,
                Page = journal.Page,
                PageSize = journal.PageSize
            });
        }

        /// <summary>
        /// Sends one event to the MAG device of a subscription.
        /// </summary>
        /// <param name="request">The subscription, the operation and its payload.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 201 with a <c>Location</c> naming the new event, 400 with the rejected fields, 400
        /// <c>mag_device_missing</c> when the subscription has no MAG device, 400 <c>csrf_invalid</c>
        /// without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>The antiforgery pair is checked first, before the body is read.</b> A request with no
        /// valid pair is not this application's request at all, and that refusal has to be the one
        /// the caller is told about - the same order every write of this API follows.
        /// </para>
        /// <para>
        /// <b>The server enforces what the operation means.</b> A <c>sendMessage</c> requires a
        /// non-blank message cut to its column and is always stored with
        /// <c>need_confirm = true</c>; a <c>playChannel</c> requires the channel as a uuid; the
        /// other four drop any message that was sent. The defaults for every unset flag are applied
        /// by the service, never by the database.
        /// </para>
        /// <para>
        /// <b>The subscription must have a MAG device.</b> That is a fact about the row rather than
        /// a malformed field, so it is refused with its own code rather than a validation map.
        /// </para>
        /// </remarks>
        [HttpPost]
        [Authorize(Policy = AuthenticationExtensions.EditSubscriptionsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SendMagEvent(
            [FromBody] AdminMagEventSendRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var subscription = ParseIdentifier(request.SubscriptionId, "subscriptionId", errors);

            // A blank identifier answers null without recording an error, because its other
            // callers treat a blank as "not named". A send has no "not named" state: an event
            // without a subscription is a row nobody can be told about, so the refusal is recorded
            // here rather than letting the null reach the value and fail as a 500.
            if (subscription is null && !errors.ContainsKey("subscriptionId"))
            {
                errors["subscriptionId"] =
                [
                    "The subscription must be the identifier of a subscription, as a uuid."
                ];
            }

            var eventType = ParseEventType(request.Event, errors);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var result = await _events.SendAsync(
                new NewMagEvent
                {
                    SubscriptionId = subscription!.Value,
                    EventType = eventType!.Value,
                    Message = request.Message,
                    NeedConfirm = request.NeedConfirm,
                    RebootAfterOk = request.RebootAfterOk,
                    AutoHideTimeout = request.AutoHideTimeout,
                    AdditionalServicesOn = request.AdditionalServicesOn,
                    Anec = request.Anec,
                    Vclub = request.Vclub
                },
                cancellationToken);

            if (result.MagDeviceMissing)
            {
                return new ObjectResult(new MessageResponse
                {
                    Code = MagEventResponses.MagDeviceMissingCode,
                    Message = "The subscription has no MAG device, so there is nothing to send " +
                        "the event to."
                })
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
            }

            if (result.Errors is { Count: > 0 })
            {
                return ApiResults.ValidationFailed(
                    new Dictionary<string, string[]>(result.Errors, StringComparer.Ordinal));
            }

            Logger.LogInformation(
                "Sent MAG event {EventId} of type {EventType} under subscription {SubscriptionId}.",
                result.EventId,
                eventType,
                subscription);

            Response.Headers.Location = $"/admin/mag-events/{result.EventId}";

            return StatusCode(
                StatusCodes.Status201Created,
                new MessageResponse
                {
                    Code = MagEventResponses.SentCode,
                    Message = "The event has been sent to the MAG device."
                });
        }

        /// <summary>
        /// Deletes every row of the journal - every subscription, every operation, whatever the
        /// screen was filtered to.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>mag_events_cleared</c>, 400 <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>It ignores any query string on purpose.</b> The button says "clear the journal", so
        /// the whole table goes whatever the reader had narrowed it to; a delete that obeyed a
        /// filter the caller happened not to send would wipe a different set from the one it named.
        /// </para>
        /// <para>
        /// <b>An empty table is success, not a refusal.</b> The second press of a button that
        /// already did its job answers 200 again with a count of zero, because the state the caller
        /// asked for is the state that holds.
        /// </para>
        /// <para>
        /// <b>The antiforgery pair is checked first, before anything else at all.</b> This action
        /// wipes the installation's record of what was sent; a request that did not come from this
        /// application must be refused as such.
        /// </para>
        /// </remarks>
        [HttpDelete]
        [Authorize(Policy = AuthenticationExtensions.EditSubscriptionsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ClearMagEvents(CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var removed = await _events.ClearAllAsync(cancellationToken);

            Logger.LogInformation(
                "Cleared the MAG event journal: {Removed} row(s) deleted.",
                removed);

            return Ok(new MessageResponse
            {
                Code = MagEventResponses.ClearedCode,
                Message = $"Cleared the MAG event journal: {removed} row(s) deleted."
            });
        }

        /// <summary>
        /// Answers <c>405</c> to the per-row methods this resource does not own.
        /// </summary>
        /// <param name="id">Identifier in the path; deliberately never read.</param>
        /// <returns>405 <c>method_not_allowed</c>.</returns>
        /// <remarks>
        /// <b>The fence is the absence of a handler, and this declares only that absence.</b> An
        /// event is a fact that was sent; editing or deleting one would rewrite the record of what
        /// happened. The collection's own <c>PATCH</c>/<c>PUT</c> reach the same answer from the
        /// framework, but a <c>{id}</c> path names no route at all, and the contract answers it with
        /// 405 rather than the 404 an unknown path would otherwise produce. Nothing here reads the
        /// body or the identifier, and nothing is written.
        /// </remarks>
        [AcceptVerbs("PATCH", "PUT", "DELETE", Route = "{id}")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status405MethodNotAllowed)]
        public IActionResult RejectRowMethod([FromRoute] string id)
        {
            _ = id;

            return StatusCode(
                StatusCodes.Status405MethodNotAllowed,
                new MessageResponse
                {
                    Code = "method_not_allowed",
                    Message = "The method is not allowed for this resource."
                });
        }

        /// <summary>Reads an optional uuid, recording a value that is not one.</summary>
        private static Guid? ParseIdentifier(
            string? value,
            string field,
            Dictionary<string, string[]> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (Guid.TryParse(value, out var parsed))
            {
                return parsed;
            }

            errors[field] = ["That value must be the identifier of the item it names, as a uuid."];
            return null;
        }

        /// <summary>
        /// Reads the operation a body names, recording a value that names no member or none at all.
        /// </summary>
        private static MagEventType? ParseEventType(
            string? value,
            Dictionary<string, string[]> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors["event"] = ["The event must be one of: playChannel, closePortal, " +
                    "reloadPortal, rebootDevice, sendMessage, resetStbLock."];

                return null;
            }

            var parsed = MagEventQueryText.ParseEventType(value);

            if (parsed is null)
            {
                errors["event"] = ["The event must be one of: playChannel, closePortal, " +
                    "reloadPortal, rebootDevice, sendMessage, resetStbLock."];
            }

            return parsed;
        }

        private static void MergeListErrors(
            Dictionary<string, string[]> target,
            Dictionary<string, string[]> source)
        {
            foreach (var entry in source)
            {
                target[entry.Key] = entry.Value;
            }
        }

        private static AdminMagEventItemResponse ToResponse(MagEventListItem item) => new()
        {
            Id = item.Id.ToString(),
            SubscriptionId = item.SubscriptionId.ToString(),
            MagMac = item.MagMac,
            EventType = MagEventQueryText.NameOf(item.EventType),
            Message = item.Message,
            Status = MagEventQueryText.NameOf(item.Status),
            CreatedAt = item.CreatedAt
        };
    }
}
