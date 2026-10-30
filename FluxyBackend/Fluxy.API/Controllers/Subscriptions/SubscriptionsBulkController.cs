using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Subscriptions;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.API.Security;
using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Interfaces;
using Fluxy.Core.Subscriptions.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Subscriptions
{
    /// <summary>
    /// The collective operations on the subscriptions resource, split from
    /// <see cref="SubscriptionsController"/>.
    /// </summary>
    /// <remarks>
    /// The seam is the shape of the request and of the answer, not the number of lines: a bulk
    /// request names a set of rows a table assembled on screen rather than one row with an address
    /// of its own, and it answers with a per-identifier report
    /// (<c>BulkOperationResponse</c>) where the rest of the resource answers with one outcome for
    /// one row. The route is <c>POST /admin/subscriptions/bulk</c>.
    ///
    /// The class policy is the permission the write beside it carries, because a bulk operation is
    /// one such write applied to several rows at once.
    /// </remarks>
    [ApiController]
    [Route("admin/subscriptions/bulk")]
    [Authorize(Policy = AuthenticationExtensions.EditSubscriptionsPolicy)]
    [Produces("application/json")]
    public sealed class SubscriptionsBulkController : ApiControllerBase
    {
        /// <summary>Names the operations a bulk body may ask for.</summary>
        private const string ActionHint =
            "The action must be 'change-tariff', 'change-creator', 'delete', 'delete-playlist', " +
            "'delete-mag', 'delete-enigma2', 'prolong' or 'block-owner'.";

        /// <summary>Sentence a plan that is not an identifier gets.</summary>
        private const string PlanHint =
            "The tariff plan must be the identifier of a plan, as a uuid.";

        /// <summary>Sentence an account that is not an identifier gets.</summary>
        private const string ResellerHint =
            "The issuer must be the identifier of an account, as a uuid.";

        /// <summary>Sentence a set of identifiers that is not all uuids gets.</summary>
        private const string IdsHint = "Every identifier must be the uuid of a subscription.";

        private readonly ISubscriptionBulkService _subscriptions;
        private readonly RequestStandingReader _standing;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionsBulkController"/> class.
        /// </summary>
        /// <param name="subscriptions">Service that applies the operation to the named rows.</param>
        /// <param name="standing">
        /// Reader of the account behind the request, for the permissions the owner block needs to
        /// reach the account it is about.
        /// </param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public SubscriptionsBulkController(
            ISubscriptionBulkService subscriptions,
            RequestStandingReader standing,
            IAntiforgery antiforgery,
            ILogger<SubscriptionsBulkController> logger)
            : base(antiforgery, logger)
        {
            _subscriptions = subscriptions;
            _standing = standing;
        }

        /// <summary>
        /// Applies one operation to every subscription the request names, and reports how each one
        /// ended.
        /// </summary>
        /// <param name="request">Which operation, over which subscriptions, and the value it writes.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 with one entry per identifier, 400 when the body broke a rule on itself (an unknown
        /// operation, no identifiers, more identifiers than one page holds, or a missing
        /// destination), 401 when the token carries no account, 403 from the policy on the way in,
        /// and 400 <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>A recorded departure from the REST shape of the rest of the resource</b>, the same one
        /// <c>VideoStreamsBulkController</c> records: the path carries a verb because the resource
        /// it would otherwise name does not exist. "These twenty subscriptions" is a set the table
        /// on screen assembled in the browser, not a row with an address of its own.
        /// </para>
        /// <para>
        /// <b>The 200 describes the run, not every row in it.</b> What happened to a particular
        /// subscription is stated in its own entry with the same code the one-row endpoint gives,
        /// plus a warning per service a plan change cleared. A refused row answers with
        /// <c>validation_failed</c> naming the field, and the rest of the selection continues.
        /// </para>
        /// <para>
        /// No captcha and no attempt window, for the reason the rest of this area gives: both guard
        /// a request with no session at all, and everything here is behind an admin session, the
        /// edit permission and a fresh antiforgery pair.
        /// </para>
        /// </remarks>
        [HttpPost]
        [ProducesResponseType<BulkOperationResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> BulkSubscriptions(
            [FromBody] BulkSubscriptionsRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (User.UserId() is not { } actingUserId)
            {
                return ApiResults.Anonymous();
            }

            // Everything the body can be wrong about is read first, so one answer names every
            // mistake rather than one per round trip - and so that no row is touched by a request
            // that is about to be refused for another reason.
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var action = ParseAction(request.Action);
            if (action is null)
            {
                errors[nameof(BulkSubscriptionsRequest.Action)] = [ActionHint];
            }

            var ids = ParseIds(request.Ids, errors);

            Guid? plan = null;
            if (action is BulkSubscriptionAction.ChangeTariff)
            {
                plan = ParseIdentifier(
                    request.TariffPlanId,
                    nameof(BulkSubscriptionsRequest.TariffPlanId),
                    PlanHint,
                    errors);
            }

            Guid? reseller = null;
            if (action is BulkSubscriptionAction.ChangeCreator)
            {
                reseller = ParseIdentifier(
                    request.ResellerId,
                    nameof(BulkSubscriptionsRequest.ResellerId),
                    ResellerHint,
                    errors);
            }

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var granted = await _standing.ReadGrantsAsync(User);

            return Ok(BulkResponses.Describe(await _subscriptions.BulkAsync(
                new BulkSubscriptionOperation
                {
                    Action = action!.Value,
                    SubscriptionIds = ids!,
                    TariffPlanId = plan,
                    ResellerId = reseller
                },
                actingUserId,
                granted,
                cancellationToken)));
        }

        /// <summary>
        /// Turns the text a bulk body sent into the operation it names, or nothing at all.
        /// </summary>
        /// <remarks>
        /// Compared after lowering, with hyphens in the spelling the rest of this API uses, so
        /// <c>Delete</c> and <c>change-tariff</c> are both accepted. Enum binding is deliberately
        /// not used, for the reason the accounts bulk endpoint gives: it would refuse the
        /// conventional lower-case form.
        /// </remarks>
        private static BulkSubscriptionAction? ParseAction(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "change-tariff" => BulkSubscriptionAction.ChangeTariff,
                "change-creator" => BulkSubscriptionAction.ChangeCreator,
                "delete" => BulkSubscriptionAction.Delete,
                "delete-playlist" => BulkSubscriptionAction.DeletePlaylist,
                "delete-mag" => BulkSubscriptionAction.DeleteMag,
                "delete-enigma2" => BulkSubscriptionAction.DeleteEnigma2,
                "prolong" => BulkSubscriptionAction.Prolong,
                "block-owner" => BulkSubscriptionAction.BlockOwner,
                _ => null
            };

        /// <summary>Reads a required uuid the operation cannot run without.</summary>
        private static Guid? ParseIdentifier(
            string? value,
            string field,
            string hint,
            Dictionary<string, string[]> errors)
        {
            if (Guid.TryParse(value, out var parsed))
            {
                return parsed;
            }

            errors[field] = [hint];
            return null;
        }

        /// <summary>
        /// Turns the identifiers a bulk body sent into the rows they name, or nothing at all once
        /// one of them is wrong.
        /// </summary>
        /// <remarks>
        /// An empty list is refused rather than answered with an empty run - an operation over zero
        /// subscriptions is a success that changed nothing, and a client that meant to send twenty
        /// deserves to be told its body did not arrive. Duplicates collapse rather than being
        /// refused: a page cannot hold the same row twice, so a duplicate can only come from a
        /// client assembling the list by hand.
        /// </remarks>
        private static IReadOnlyList<Guid>? ParseIds(
            IReadOnlyList<string>? ids,
            Dictionary<string, string[]> errors)
        {
            if (ids is null || ids.Count is 0)
            {
                errors[nameof(BulkSubscriptionsRequest.Ids)] = ["Select at least one subscription."];
                return null;
            }

            if (ids.Count > SubscriptionBulkLimits.MaxItems)
            {
                errors[nameof(BulkSubscriptionsRequest.Ids)] =
                [$"At most {SubscriptionBulkLimits.MaxItems} subscriptions may be named in one request."];
                return null;
            }

            var parsed = new List<Guid>(ids.Count);

            foreach (var text in ids)
            {
                if (!Guid.TryParse(text, out var id))
                {
                    errors[nameof(BulkSubscriptionsRequest.Ids)] = [IdsHint];
                    return null;
                }

                if (!parsed.Contains(id))
                {
                    parsed.Add(id);
                }
            }

            return parsed;
        }
    }
}
