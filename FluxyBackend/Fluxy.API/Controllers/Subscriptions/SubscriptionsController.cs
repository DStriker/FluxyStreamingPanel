using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Subscriptions;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Interfaces;
using Fluxy.Core.Subscriptions.Outcomes;
using Fluxy.Core.Subscriptions.ReadModels;
using Fluxy.Core.Subscriptions.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Subscriptions
{
    /// <summary>
    /// Managing the subscriptions - the rows an operator issues to a user.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One policy on the class (<c>AdminOnly</c>) with the two subscription permissions on top of
    /// it, one per action, the shape the plans controller uses: every route here is an admin
    /// route, and a read is <c>viewSubscriptions</c> while a write is <c>editSubscriptions</c>.
    /// The difference matters because looking at who holds which access and issuing it are two
    /// different jobs.
    /// </para>
    /// <para>
    /// <b>No captcha and no attempt window</b>, for the reason the accounts controller gives: both
    /// exist to guard a request with no session at all, and everything here is behind an admin
    /// session and a fresh antiforgery pair.
    /// </para>
    /// <para>
    /// <b>The list is paged, searched, filtered and sorted on the server</b>, because the pager
    /// draws a <c>total</c> and the status is computed: a filter applied in the browser would
    /// narrow the page on screen while the pager still counted everything, and the tag would be
    /// an answer the filter could not reproduce.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/subscriptions")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class SubscriptionsController : ApiControllerBase
    {
        /// <summary>Sentence a column that names nothing gets.</summary>
        private const string SortByHint =
            "The sort field must be one of: createdAt, expireAt.";

        /// <summary>Sentence a direction that names nothing gets.</summary>
        private const string SortOrderHint = "The sort order must be either asc or desc.";

        /// <summary>Sentence a status that names nothing gets.</summary>
        private const string StatusHint =
            "The status must be one of: active, trial, blocked, expired.";

        /// <summary>Sentence an identifier that is not a uuid gets.</summary>
        private const string IdentifierHint =
            "That value must be the identifier of the item it names, as a uuid.";

        /// <summary>Sentence a create with no owner gets.</summary>
        private const string OwnerHint =
            "The owner must be the identifier of an account, as a uuid.";

        /// <summary>Sentence an expiry that is neither an ISO date-time nor a bare date gets.</summary>
        private const string ExpireAtHint =
            "The expiry must be an ISO 8601 date-time (2027-01-31T14:30:00Z) " +
            "or a date written as yyyy-MM-dd.";

        /// <summary>Sentence a format that names no member gets.</summary>
        private const string OutputFormatsHint =
            "Every format must be one of: mpegTs, hls.";

        /// <summary>Sentence a bouquet set that is not all uuids gets.</summary>
        private const string BouquetIdsHint =
            "Every identifier must be the uuid of an existing bouquet.";

        private readonly ISubscriptionService _subscriptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionsController"/> class.
        /// </summary>
        /// <param name="subscriptions">Service that reads and changes the subscriptions.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public SubscriptionsController(
            ISubscriptionService subscriptions,
            IAntiforgery antiforgery,
            ILogger<SubscriptionsController> logger)
            : base(antiforgery, logger)
        {
            _subscriptions = subscriptions;
        }

        /// <summary>
        /// Reports one page of the table, filtered and ordered the way the query asked.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many subscriptions one page holds.</param>
        /// <param name="search">Substring of the playlist id or either MAC, or null.</param>
        /// <param name="ownerId">Owner to keep, as a uuid, or null.</param>
        /// <param name="creatorId">Issuer to keep, as a uuid, or null.</param>
        /// <param name="status">Computed state to keep; <c>createdAt</c> order when absent.</param>
        /// <param name="sortBy">Column to order by; <c>createdAt</c> when absent.</param>
        /// <param name="sortOrder">Direction; <c>desc</c> when absent.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with one page, or 400 naming the bounds that were broken.</returns>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewSubscriptionsPolicy)]
        [ProducesResponseType<AdminSubscriptionPageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSubscriptions(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = SubscriptionLimits.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] string? ownerId = null,
            [FromQuery] string? creatorId = null,
            [FromQuery] string? status = null,
            [FromQuery] string? sortBy = "createdAt",
            [FromQuery] string? sortOrder = "desc",
            CancellationToken cancellationToken = default)
        {
            NoStore();

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var field = SubscriptionTextParser.ParseSortField(sortBy);
            var order = SubscriptionTextParser.ParseSortOrder(sortOrder);
            var wantedStatus = ParseStatus(status, errors);
            var owner = ParseIdentifier(ownerId, "ownerId", errors);
            var creator = ParseIdentifier(creatorId, "creatorId", errors);

            MergeListErrors(
                errors,
                ListQueryErrors.Common(
                    page,
                    pageSize,
                    search,
                    SubscriptionLimits.MaxPageSize,
                    SubscriptionLimits.MaxSearchLength));

            if (field is null)
            {
                errors["sortBy"] = [SortByHint];
            }

            if (order is null)
            {
                errors["sortOrder"] = [SortOrderHint];
            }

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var list = await _subscriptions.GetPageAsync(
                page,
                pageSize,
                search,
                owner,
                creator,
                wantedStatus,
                field!.Value,
                order!.Value,
                cancellationToken);

            return Ok(new AdminSubscriptionPageResponse
            {
                Items = list.Items.Select(ToResponse).ToList(),
                Total = list.Total,
                Page = list.Page,
                PageSize = list.PageSize
            });
        }

        /// <summary>Reports one subscription in full, for the edit form.</summary>
        /// <param name="id">Identifier of the subscription, in the path.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with the subscription, or 404 when there is no such subscription.</returns>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.ViewSubscriptionsPolicy)]
        [ProducesResponseType<AdminSubscriptionDetailResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSubscription(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            NoStore();

            var subscription = await _subscriptions.GetAsync(id, cancellationToken);

            return subscription is null
                ? Answer(new SubscriptionOutcome
                {
                    Action = SubscriptionAction.NotFound,
                    SubscriptionId = id
                })
                : Ok(ToDetailResponse(subscription));
        }

        /// <summary>Issues a subscription.</summary>
        /// <param name="request">The subscription to create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 201 with a <c>Location</c> naming the new subscription, 400 with the rejected fields,
        /// 409 when another subscription holds that playlist id or MAC, 400 <c>csrf_invalid</c>
        /// without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// The issuer is stamped from the session and never from the body: a body naming one is
        /// not obeyed, so an operator cannot issue a subscription in somebody else's name by
        /// editing a request.
        /// </remarks>
        [HttpPost]
        [Authorize(Policy = AuthenticationExtensions.EditSubscriptionsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateSubscription(
            [FromBody] CreateSubscriptionRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (User.UserId() is not { } issuer)
            {
                return ApiResults.Anonymous();
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var owner = ParseIdentifier(request.UserId, nameof(request.UserId), errors);

            // A create must name an owner, and `ParseIdentifier` answers null for a blank value
            // *without* recording an error - deliberately, because its other callers treat a blank
            // as "not named" (the list filters, the update's plan). A create has no "not named"
            // state: a subscription without an owner is a row nobody can be told about, so the
            // refusal is recorded here rather than letting the null reach `owner!.Value` and fail
            // as a 500.
            if (owner is null && !errors.ContainsKey(nameof(CreateSubscriptionRequest.UserId)))
            {
                errors[nameof(CreateSubscriptionRequest.UserId)] = [OwnerHint];
            }

            var plan = ParseIdentifier(request.TariffPlanId, nameof(request.TariffPlanId), errors);
            var expireAt = ParseExpireAt(request.ExpireAt, nameof(request.ExpireAt), errors);
            var formats = ParseOutputFormats(
                request.OutputFormats,
                nameof(request.OutputFormats),
                errors);
            var bouquets = ParseIdentifiers(
                request.BouquetIds,
                nameof(request.BouquetIds),
                errors);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _subscriptions.CreateAsync(
                new NewSubscription
                {
                    UserId = owner!.Value,
                    ResellerId = issuer,
                    PlaylistId = request.PlaylistId,
                    ExpireAt = expireAt,
                    TariffPlanId = plan,
                    MaxConnections = request.MaxConnections,
                    IsRestreamer = request.IsRestreamer,
                    IpWhitelist = request.IpWhitelist ?? [],
                    UserAgentWhitelist = request.UserAgentWhitelist ?? [],
                    IsIspLock = request.IsIspLock,
                    IsCountryLock = request.IsCountryLock,
                    IsTrial = request.IsTrial,
                    OutputFormats = formats ?? SubscriptionOutputFormat.MpegTs,
                    MagMac = request.MagMac,
                    IsStbLock = request.IsStbLock,
                    Enigma2Mac = request.Enigma2Mac,

                    // Left as the caller sent them, including `null`. The difference is the
                    // domain's: an absent list means "every bouquet the installation has", while
                    // an empty one is the operator's own statement and is refused by the
                    // minimum-one rule while no plan is attached.
                    BouquetIds = bouquets
                },
                cancellationToken);

            var (statusCode, body) = SubscriptionResponses.Describe(outcome);

            if (statusCode == StatusCodes.Status201Created)
            {
                Response.Headers.Location = $"/admin/subscriptions/{outcome.SubscriptionId}";
            }

            return StatusCode(statusCode, body);
        }

        /// <summary>Changes a subscription. Every field the body can name is optional.</summary>
        /// <param name="id">Identifier of the subscription to change, in the path.</param>
        /// <param name="request">The fields to change. An absent field is left alone.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 when it was written, 400 with the rejected fields, 404 when there is no such
        /// subscription, 409 when another subscription holds that playlist id or MAC.
        /// </returns>
        [HttpPatch("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditSubscriptionsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateSubscription(
            [FromRoute] Guid id,
            [FromBody] UpdateSubscriptionRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            // A blank plan id is "not named" and keeps the row's own; anything else has to name a
            // plan. Clearing is its own flag, because a null cannot say "set" and "clear" apart.
            var plan = string.IsNullOrWhiteSpace(request.TariffPlanId)
                ? null
                : ParseIdentifier(request.TariffPlanId, nameof(request.TariffPlanId), errors);

            var expireAt = request.ExpireAt is null
                ? null
                : ValidateExpireAt(request.ExpireAt, nameof(request.ExpireAt), errors);

            var formats = ParseOutputFormats(
                request.OutputFormats,
                nameof(request.OutputFormats),
                errors);

            var bouquets = request.BouquetIds is null
                ? null
                : ParseIdentifiers(request.BouquetIds, nameof(request.BouquetIds), errors);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _subscriptions.UpdateAsync(
                id,
                new SubscriptionPatch
                {
                    PlaylistId = request.PlaylistId,
                    ExpireAt = expireAt,

                    // "Not named" and "cleared" are two requests, so the flag travels beside the
                    // identifier rather than being inferred from a null.
                    TariffPlanId = plan,
                    ClearTariffPlan = request.ClearTariffPlan,
                    MaxConnections = request.MaxConnections,
                    ClearMaxConnections = request.ClearMaxConnections,
                    IsRestreamer = request.IsRestreamer,
                    IpWhitelist = request.IpWhitelist,
                    UserAgentWhitelist = request.UserAgentWhitelist,
                    IsIspLock = request.IsIspLock,
                    IsCountryLock = request.IsCountryLock,
                    IsTrial = request.IsTrial,
                    OutputFormats = formats,
                    MagMac = request.MagMac,
                    IsStbLock = request.IsStbLock,
                    Enigma2Mac = request.Enigma2Mac,
                    BouquetIds = bouquets
                },
                cancellationToken);

            return Answer(outcome);
        }

        /// <summary>Deletes a subscription.</summary>
        /// <param name="id">Identifier of the subscription to delete, in the path.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 when it was deleted, 404 when there was no such subscription.</returns>
        /// <remarks>
        /// A <c>DELETE</c> that answers 200 rather than 204, because it has something to say: the
        /// difference between a subscription that was deleted and one that was not there is the
        /// whole answer. The bouquet links go with it through the foreign key.
        /// </remarks>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditSubscriptionsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteSubscription(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await _subscriptions.DeleteAsync(id, cancellationToken));
        }

        /// <summary>
        /// Extends the subscription's period from its end by its plan's duration.
        /// </summary>
        /// <param name="id">Identifier of the subscription, in the path.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>subscription_prolonged</c>, 400 <c>validation_failed</c> naming the plan field
        /// when the subscription has no plan to prolong by, or 404.
        /// </returns>
        /// <remarks>
        /// The answer is the code and the sentence alone; the new expiry is read back from
        /// <c>GET /admin/subscriptions/{id}</c>, which is what the table does after every write.
        /// 
        /// <remarks>
        /// <para>
        /// <b>A recorded departure from the REST shape of this resource</b>, the one the accounts
        /// area records for <c>POST {id}/block</c>: a prolongation is a state transition with rules
        /// of its own - it reads the current expiry, keeps whatever time is left and refuses a
        /// plan-less row - and putting that into a <c>PATCH</c> would make the caller responsible
        /// for computing the new date. As its own action it can answer with its own code and its
        /// own refusal.
        /// </para>
        /// <para>
        /// The bulk endpoint's <c>prolong</c> action delegates here, so a row and a selection give
        /// the same answer for the same subscription.
        /// </para>
        /// </remarks>
        [HttpPost("{id:guid}/prolong")]
        [Authorize(Policy = AuthenticationExtensions.EditSubscriptionsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ProlongSubscription(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await _subscriptions.ProlongAsync(id, cancellationToken));
        }

        /// <summary>
        /// Mints a playlist id no subscription holds yet, without writing anything.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with a fresh 32-hex identifier.</returns>
        /// <remarks>
        /// <para>
        /// <b>A recorded departure from the REST shape of the resource.</b> The route is
        /// <c>POST .../playlist-id</c> and it writes no resource: it mints a value the create form
        /// and the regenerate action need to show before saving. There is no noun to GET - the
        /// identifier does not exist until the operator's save stores it - and a GET that minted
        /// one would be a cacheable request with a different answer every time, which is the one
        /// thing a GET must not be. The same departure <c>POST /auth/csrf</c> records, for the
        /// same reason.
        /// </para>
        /// <para>
        /// It carries the edit permission and a fresh antiforgery pair because it mints something
        /// the operator is about to store: an anonymous caller has no business being handed an
        /// identifier this installation will accept.
        /// </para>
        /// </remarks>
        [HttpPost("playlist-id")]
        [Authorize(Policy = AuthenticationExtensions.EditSubscriptionsPolicy)]
        [ProducesResponseType<SubscriptionPlaylistIdResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GeneratePlaylistId(CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Ok(new SubscriptionPlaylistIdResponse
            {
                PlaylistId = await _subscriptions.GeneratePlaylistIdAsync(cancellationToken)
            });
        }

        /// <summary>
        /// Turns the service's answer into the status and body a client is told, the one place
        /// where a <see cref="SubscriptionAction"/> becomes a number.
        /// </summary>
        private static IActionResult Answer(SubscriptionOutcome outcome)
        {
            var (status, body) = SubscriptionResponses.Describe(outcome);

            return new ObjectResult(body) { StatusCode = status };
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

            errors[field] = [IdentifierHint];
            return null;
        }

        /// <summary>Reads an optional list of uuids, recording the set when one is not one.</summary>
        private static IReadOnlyList<Guid>? ParseIdentifiers(
            IReadOnlyList<string>? values,
            string field,
            Dictionary<string, string[]> errors)
        {
            if (values is null)
            {
                return null;
            }

            var parsed = new List<Guid>(values.Count);

            foreach (var value in values)
            {
                if (!Guid.TryParse(value, out var id))
                {
                    errors[field] = [BouquetIdsHint];
                    return null;
                }

                if (!parsed.Contains(id))
                {
                    parsed.Add(id);
                }
            }

            return parsed;
        }

        /// <summary>
        /// Reads a status filter, recording a value that names no member.
        /// </summary>
        /// <remarks>
        /// An empty value is "no filter", which is the same request as an absent parameter; any
        /// other value has to name a member, because a filter that silently fell back to "every
        /// state" would answer a request the operator did not make.
        /// </remarks>
        private static SubscriptionStatus? ParseStatus(
            string? value,
            Dictionary<string, string[]> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var parsed = SubscriptionTextParser.ParseStatus(value);

            if (parsed is null)
            {
                errors["status"] = [StatusHint];
            }

            return parsed;
        }

        /// <summary>
        /// Reads the expiry a <b>create</b> names: blank is "never", a value must be a date.
        /// </summary>
        private static DateTimeOffset? ParseExpireAt(
            string? value,
            string field,
            Dictionary<string, string[]> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var text = ValidateExpireAt(value, field, errors);

            return text.Length == 0 ? null : SubscriptionTextParser.ParseExpireAt(text);
        }

        /// <summary>
        /// Reads the expiry a <b>change</b> names: blank clears it, a value must be a date.
        /// </summary>
        /// <remarks>
        /// The two differ by what blank means, which is the whole reason the transport has two
        /// readers: on a create there is nothing to clear, and on a change an empty string is the
        /// operator asking for "never expires".
        /// </remarks>
        private static string ValidateExpireAt(
            string value,
            string field,
            Dictionary<string, string[]> errors)
        {
            if (string.IsNullOrWhiteSpace(value) || SubscriptionTextParser.ParseExpireAt(value) is not null)
            {
                return value;
            }

            errors[field] = [ExpireAtHint];
            return string.Empty;
        }

        /// <summary>
        /// Reads the formats a body names, recording a name that maps to none or an empty list.
        /// </summary>
        private static SubscriptionOutputFormat? ParseOutputFormats(
            IReadOnlyList<string>? values,
            string field,
            Dictionary<string, string[]> errors)
        {
            if (values is null)
            {
                return null;
            }

            if (SubscriptionTextParser.TryParseOutputFormats(values, out var formats) is false
                || formats == SubscriptionOutputFormat.None)
            {
                errors[field] = [OutputFormatsHint];
                return null;
            }

            return formats;
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

        private static AdminSubscriptionItemResponse ToResponse(SubscriptionListItem item) => new()
        {
            Id = item.Id.ToString(),
            UserId = item.UserId.ToString(),
            Username = item.Username,
            OwnerBlocked = item.OwnerBlocked,
            CreatorUsername = item.CreatorUsername,
            TariffPlanId = item.TariffPlanId?.ToString(),
            TariffPlanName = item.TariffPlanName,
            TariffPlanDurationValue = item.TariffPlanDurationValue,
            TariffPlanDurationUnit = item.TariffPlanDurationUnit is { } unit
                ? TariffPlanTextParser.NameOf(unit)
                : null,
            CreatedAt = item.CreatedAt,
            ExpireAt = item.ExpireAt,
            IsTrial = item.IsTrial,
            Status = SubscriptionTextParser.NameOf(item.Status),
            MaxConnections = item.MaxConnections,
            PlaylistId = item.PlaylistId,
            MagMac = item.MagMac,
            Enigma2Mac = item.Enigma2Mac,
            SupportsPlaylist = item.SupportsPlaylist,
            SupportsMag = item.SupportsMag,
            SupportsEnigma2 = item.SupportsEnigma2
        };

        private static AdminSubscriptionDetailResponse ToDetailResponse(SubscriptionDetail detail) => new()
        {
            Id = detail.Id.ToString(),
            UserId = detail.UserId.ToString(),
            Username = detail.Username,
            ResellerId = detail.ResellerId?.ToString(),
            CreatorUsername = detail.CreatorUsername,
            PlaylistId = detail.PlaylistId,
            ExpireAt = detail.ExpireAt,
            TariffPlanId = detail.TariffPlanId?.ToString(),
            TariffPlanName = detail.TariffPlanName,
            Status = SubscriptionTextParser.NameOf(detail.Status),
            MaxConnections = detail.MaxConnections,
            IsRestreamer = detail.IsRestreamer,
            IpWhitelist = detail.IpWhitelist,
            UserAgentWhitelist = detail.UserAgentWhitelist,
            IsIspLock = detail.IsIspLock,
            IsCountryLock = detail.IsCountryLock,
            IsTrial = detail.IsTrial,
            OutputFormats = SubscriptionTextParser.NameOf(detail.OutputFormats),
            MagMac = detail.MagMac,
            IsStbLock = detail.IsStbLock,
            Enigma2Mac = detail.Enigma2Mac,
            BouquetIds = detail.BouquetIds.Select(id => id.ToString()).ToList(),
            CreatedAt = detail.CreatedAt,
            Derived = detail.Derived is null
                ? null
                : new AdminSubscriptionDerivedResponse
                {
                    MaxConnections = detail.Derived.MaxConnections,
                    IsRestreamer = detail.Derived.IsRestreamer,
                    OutputFormats = SubscriptionTextParser.NameOf(detail.Derived.OutputFormats),
                    IsStbLock = detail.Derived.IsStbLock,
                    IsCountryLock = detail.Derived.IsCountryLock,
                    IsTrial = detail.Derived.IsTrial,
                    SupportsPlaylist = detail.Derived.SupportsPlaylist,
                    SupportsMag = detail.Derived.SupportsMag,
                    SupportsEnigma2 = detail.Derived.SupportsEnigma2,
                    BouquetIds = detail.Derived.BouquetIds.Select(id => id.ToString()).ToList()
                }
        };
    }
}
