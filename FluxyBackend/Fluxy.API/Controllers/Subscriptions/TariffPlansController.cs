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
    /// Managing the tariff plans - the packages a subscription is sold under.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One policy on the class (<c>AdminOnly</c>) with the two plan permissions on top of it, one
    /// per action, the shape the bouquets controller uses: every route here is an admin route,
    /// and a read is <c>viewTariffPlans</c> while a write is <c>editTariffPlans</c>. The
    /// difference matters because pricing a subscription is a job that can be delegated apart
    /// from deciding what content it delivers.
    /// </para>
    /// <para>
    /// <b>No captcha and no attempt window</b>, for the reason the accounts controller gives:
    /// both exist to guard a request with no session at all, and everything here is behind an
    /// admin session and a fresh antiforgery pair.
    /// </para>
    /// <para>
    /// <b>The list is paged, searched and sorted on the server</b>, because the pager draws a
    /// <c>total</c>: a filter or an order applied in the browser would narrow the page on screen
    /// while the pager still counted everything. The bounds are refused rather than clamped, and
    /// a value that names nothing is refused rather than treated as "no filter".
    /// </para>
    /// <para>
    /// Both reads carry <c>Cache-Control: no-store</c> through <see cref="ApiControllerBase.NoStore"/>:
    /// this is the structure of a private area, and a shared cache that kept one would hand it to
    /// the next visitor.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/tariff-plans")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class TariffPlansController : ApiControllerBase
    {
        /// <summary>Sentence a column that names nothing gets.</summary>
        private const string SortByHint =
            "The sort field must be one of: name, cost, createdAt.";

        /// <summary>Sentence a direction that names nothing gets.</summary>
        private const string SortOrderHint = "The sort order must be either asc or desc.";

        /// <summary>Sentence a unit that names nothing gets.</summary>
        private const string DurationUnitHint =
            "The validity unit must be one of: hours, days, months, years.";

        private readonly ITariffPlanService _plans;

        /// <summary>
        /// Initializes a new instance of the <see cref="TariffPlansController"/> class.
        /// </summary>
        /// <param name="plans">Service that reads and changes the plans.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public TariffPlansController(
            ITariffPlanService plans,
            IAntiforgery antiforgery,
            ILogger<TariffPlansController> logger)
            : base(antiforgery, logger)
        {
            _plans = plans;
        }

        /// <summary>
        /// Reports one page of the table, filtered and ordered the way the query asked.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many plans one page holds.</param>
        /// <param name="search">Case-insensitive substring of the name, or null.</param>
        /// <param name="sortBy">Column to order by; <c>name</c> when absent.</param>
        /// <param name="sortOrder">Direction; <c>asc</c> when absent.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with one page, or 400 naming the bounds that were broken.</returns>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewTariffPlansPolicy)]
        [ProducesResponseType<AdminTariffPlanPageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetPlans(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = TariffPlanLimits.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = "name",
            [FromQuery] string? sortOrder = "asc",
            CancellationToken cancellationToken = default)
        {
            NoStore();

            var field = TariffPlanTextParser.ParseSortField(sortBy);
            var order = TariffPlanTextParser.ParseSortOrder(sortOrder);

            if (page < 1
                || pageSize is < 1 or > TariffPlanLimits.MaxPageSize
                || (search?.Length ?? 0) > TariffPlanLimits.MaxSearchLength
                || field is null
                || order is null)
            {
                return ApiResults.ValidationFailed(ListErrors(page, pageSize, search, field, order));
            }

            var list = await _plans.GetPageAsync(
                page,
                pageSize,
                search,
                field.Value,
                order.Value,
                cancellationToken);

            return Ok(new AdminTariffPlanPageResponse
            {
                Items = list.Items.Select(ToResponse).ToList(),
                Total = list.Total,
                Page = list.Page,
                PageSize = list.PageSize
            });
        }

        /// <summary>Reports one plan in full, for the edit form.</summary>
        /// <param name="id">Identifier of the plan, in the path.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with the plan, or 404 when there is no such plan.</returns>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.ViewTariffPlansPolicy)]
        [ProducesResponseType<AdminTariffPlanDetailResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPlan(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            NoStore();

            var plan = await _plans.GetAsync(id, cancellationToken);

            return plan is null
                ? Answer(new TariffPlanOutcome { Action = TariffPlanAction.NotFound, PlanId = id })
                : Ok(ToDetailResponse(plan));
        }

        /// <summary>
        /// Creates a plan.
        /// </summary>
        /// <param name="request">The plan to create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 201 with a <c>Location</c> naming the new plan, 400 with the rejected fields, 409 when
        /// another plan holds that name in any spelling, 400 <c>csrf_invalid</c> without the
        /// antiforgery pair.
        /// </returns>
        /// <remarks>
        /// The 201 carries a <c>Location</c>, and it is honest: the row exists in full the moment
        /// it is written, and this very controller serves the address it names.
        /// </remarks>
        [HttpPost]
        [Authorize(Policy = AuthenticationExtensions.EditTariffPlansPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreatePlan(
            [FromBody] CreateTariffPlanRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var unit = ParseDurationUnit(request.DurationUnit, errors);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _plans.CreateAsync(
                new NewTariffPlan
                {
                    Name = request.Name?.Trim() ?? string.Empty,
                    IsTrial = request.IsTrial,
                    Cost = request.Cost,
                    DurationValue = request.DurationValue,
                    DurationUnit = unit!.Value,
                    SupportsM3u = request.SupportsM3u,
                    SupportsMag = request.SupportsMag,
                    SupportsEnigma2 = request.SupportsEnigma2,
                    OutputMpegTs = request.OutputMpegTs,
                    OutputHls = request.OutputHls,
                    StrictStbDeviceId = request.StrictStbDeviceId,
                    AllowRestream = request.AllowRestream,
                    MaxConnections = request.MaxConnections,
                    AllowMultipleCountries = request.AllowMultipleCountries,

                    // Left as the caller sent them, including `null`. The difference is the
                    // domain's: an absent group list means "every base group" (the default),
                    // while an empty one is the operator's own statement and is refused by the
                    // minimum-one rule. The bouquet list has no default, so absence and an empty
                    // list are the same refusal there.
                    UserGroupIds = request.UserGroupIds,
                    BouquetIds = request.BouquetIds ?? []
                },
                cancellationToken);

            var (statusCode, body) = TariffPlanResponses.Describe(outcome);

            if (statusCode == StatusCodes.Status201Created)
            {
                Response.Headers.Location = $"/admin/tariff-plans/{outcome.PlanId}";
            }

            return StatusCode(statusCode, body);
        }

        /// <summary>Changes a plan. Every field the body can name is optional.</summary>
        /// <param name="id">Identifier of the plan to change, in the path.</param>
        /// <param name="request">The fields to change. An absent field is left alone.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 when it was written, 400 with the rejected fields, 404 when there is no such plan,
        /// 409 when another plan holds that name in any spelling.
        /// </returns>
        /// <remarks>
        /// The identifier travels in the path and never in the body, for the reason the accounts
        /// endpoint gives: the path is the honest statement of which row is meant. A body that
        /// names nothing is a success rather than a complaint, the convention every other PATCH
        /// on this API follows.
        /// </remarks>
        [HttpPatch("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditTariffPlansPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdatePlan(
            [FromRoute] Guid id,
            [FromBody] UpdateTariffPlanRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            // A blank unit is "not named" and keeps the row's own; anything else has to name a
            // member. A name that is present but blank is refused rather than stored, which is
            // what makes "null keeps, blank refuses" the rule for the two text fields.
            var unit = request.DurationUnit is null
                ? null
                : ParseDurationUnit(request.DurationUnit, errors);

            if (request.Name is not null && string.IsNullOrWhiteSpace(request.Name))
            {
                errors[nameof(UpdateTariffPlanRequest.Name)] =
                    ["The name must not be blank."];
            }

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _plans.UpdateAsync(
                id,
                new TariffPlanPatch
                {
                    Name = request.Name?.Trim(),
                    IsTrial = request.IsTrial,
                    Cost = request.Cost,
                    DurationValue = request.DurationValue,
                    DurationUnit = unit,
                    SupportsM3u = request.SupportsM3u,
                    SupportsMag = request.SupportsMag,
                    SupportsEnigma2 = request.SupportsEnigma2,
                    OutputMpegTs = request.OutputMpegTs,
                    OutputHls = request.OutputHls,
                    StrictStbDeviceId = request.StrictStbDeviceId,
                    AllowRestream = request.AllowRestream,
                    MaxConnections = request.MaxConnections,
                    AllowMultipleCountries = request.AllowMultipleCountries,
                    UserGroupIds = request.UserGroupIds,
                    BouquetIds = request.BouquetIds
                },
                cancellationToken);

            return Answer(outcome);
        }

        /// <summary>Deletes a plan.</summary>
        /// <remarks>
        /// A <c>DELETE</c> that answers 200 rather than 204, because it has something to say: the
        /// difference between a plan that was deleted and one that was not there is the whole
        /// answer. The two junction rows go with it through their foreign keys.
        /// </remarks>
        /// <param name="id">Identifier of the plan to delete, in the path.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 when it was deleted, 404 when there was no such plan.</returns>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditTariffPlansPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeletePlan(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await _plans.DeleteAsync(id, cancellationToken));
        }

        /// <summary>
        /// Reads the validity unit of a create or a change. A blank value means "not named": the
        /// create has nothing to fall back on and refuses, while the patch keeps what the row
        /// holds.
        /// </summary>
        private static TariffPlanDurationUnit? ParseDurationUnit(
            string? value,
            Dictionary<string, string[]> errors)
        {
            var unit = TariffPlanTextParser.ParseDurationUnit(value);

            if (unit is null)
            {
                errors[nameof(CreateTariffPlanRequest.DurationUnit)] = [DurationUnitHint];
            }

            return unit;
        }

        /// <summary>
        /// Which of the bounds a plan list request broke, keyed by the camelCase name of the query
        /// parameter so the reason lands under the input a client would fix.
        /// </summary>
        private static Dictionary<string, string[]> ListErrors(
            int page,
            int pageSize,
            string? search,
            TariffPlanSortField? field,
            TariffPlanSortOrder? order)
        {
            var errors = ListQueryErrors.Common(
                page,
                pageSize,
                search,
                TariffPlanLimits.MaxPageSize,
                TariffPlanLimits.MaxSearchLength);

            if (field is null)
            {
                errors["sortBy"] = [SortByHint];
            }

            if (order is null)
            {
                errors["sortOrder"] = [SortOrderHint];
            }

            return errors;
        }

        /// <summary>
        /// Turns the service's answer into the status and body a client is told, the one place
        /// where a <see cref="TariffPlanAction"/> becomes a number.
        /// </summary>
        private static IActionResult Answer(TariffPlanOutcome outcome)
        {
            var (status, body) = TariffPlanResponses.Describe(outcome);

            return new ObjectResult(body) { StatusCode = status };
        }

        private static AdminTariffPlanResponse ToResponse(TariffPlanListItem plan) => new()
        {
            Id = plan.Id.ToString(),
            Name = plan.Name,
            IsTrial = plan.IsTrial,
            Cost = plan.Cost,
            DurationValue = plan.DurationValue,
            DurationUnit = TariffPlanTextParser.NameOf(plan.DurationUnit),
            SupportsM3u = plan.SupportsM3u,
            SupportsMag = plan.SupportsMag,
            SupportsEnigma2 = plan.SupportsEnigma2,
            OutputMpegTs = plan.OutputMpegTs,
            OutputHls = plan.OutputHls,
            StrictStbDeviceId = plan.StrictStbDeviceId,
            AllowRestream = plan.AllowRestream,
            MaxConnections = plan.MaxConnections,
            AllowMultipleCountries = plan.AllowMultipleCountries,
            UserGroupCount = plan.UserGroupCount,
            BouquetCount = plan.BouquetCount
        };

        private static AdminTariffPlanDetailResponse ToDetailResponse(TariffPlanDetail plan) => new()
        {
            Id = plan.Id.ToString(),
            Name = plan.Name,
            IsTrial = plan.IsTrial,
            Cost = plan.Cost,
            DurationValue = plan.DurationValue,
            DurationUnit = TariffPlanTextParser.NameOf(plan.DurationUnit),
            SupportsM3u = plan.SupportsM3u,
            SupportsMag = plan.SupportsMag,
            SupportsEnigma2 = plan.SupportsEnigma2,
            OutputMpegTs = plan.OutputMpegTs,
            OutputHls = plan.OutputHls,
            StrictStbDeviceId = plan.StrictStbDeviceId,
            AllowRestream = plan.AllowRestream,
            MaxConnections = plan.MaxConnections,
            AllowMultipleCountries = plan.AllowMultipleCountries,
            UserGroupIds = plan.UserGroupIds.Select(id => id.ToString()).ToList(),
            BouquetIds = plan.BouquetIds.Select(id => id.ToString()).ToList()
        };
    }
}
