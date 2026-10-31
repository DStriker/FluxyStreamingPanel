using Fluxy.API.Contracts.Blacklists;
using Fluxy.API.Contracts.Common;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Blacklists.Commands;
using Fluxy.Core.Blacklists.Enums;
using Fluxy.Core.Blacklists.Interfaces;
using Fluxy.Core.Blacklists.Outcomes;
using Fluxy.Core.Blacklists.ReadModels;
using Fluxy.Core.Blacklists.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Admin
{
    /// <summary>
    /// Managing the three access-control blacklists: one page of one list, one entry in full,
    /// and the writes that create, change and delete them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One controller for all three lists, because they are one resource: one table, one set of
    /// rules and one screen family. The list is a required parameter of every read rather than
    /// three routes, and the three admin pages pass it as a constant - a route per list would
    /// be three copies of five methods that differ in one argument.
    /// </para>
    /// <para>
    /// Two policies on every action: the class carries <c>AdminOnly</c> the way the other admin
    /// controllers do, and each action adds its own permission on top. Read and write are
    /// separate grants, so a group which may look at the lists and a group which may change
    /// them stay two different things.
    /// </para>
    /// <para>
    /// <b>No captcha and no attempt window</b>, for the reason the rest of the admin area
    /// gives: both guard a request with no session at all, and everything here is behind an
    /// admin session and a fresh antiforgery pair, which <see cref="ApiControllerBase.RejectsCsrfAsync"/>
    /// checks before any body is read.
    /// </para>
    /// <para>
    /// <b>The counter and the added date have no write path.</b> The counter is incremented by
    /// the enforcement that does not exist yet, and the added date is stamped once when the row
    /// is inserted. A <c>PATCH</c> therefore changes the value and the note only, whatever the
    /// body also carries.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/blacklist-entries")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class BlacklistEntriesController : ApiControllerBase
    {
        /// <summary>Sentence a list that names no known member gets.</summary>
        private const string TypeHint =
            "The type must be one of: ip, isp, user-agent.";

        /// <summary>Sentence a column that names nothing gets.</summary>
        private const string SortByHint =
            "The sort field must be one of: value, blockedattempts, createdat.";

        /// <summary>Sentence a direction that names nothing gets.</summary>
        private const string SortOrderHint = "The sort order must be either asc or desc.";

        private readonly IBlacklistEntryService _entries;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlacklistEntriesController"/> class.
        /// </summary>
        /// <param name="entries">Service that reads and changes the entries.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public BlacklistEntriesController(
            IBlacklistEntryService entries,
            IAntiforgery antiforgery,
            ILogger<BlacklistEntriesController> logger)
            : base(antiforgery, logger)
        {
            _entries = entries;
        }

        /// <summary>
        /// Reports one page of one list, filtered and ordered the way the query asked.
        /// </summary>
        /// <remarks>
        /// Search, order and paging live here rather than in the browser because the pager
        /// shows a <c>total</c>. The bounds are refused rather than clamped, and a value that
        /// names nothing is refused rather than treated as "no filter" - a request without a
        /// list would fold three unrelated screens into one answer no screen draws.
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewBlacklistsPolicy)]
        [ProducesResponseType<AdminBlacklistEntryPageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetEntries(
            [FromQuery] string? type = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = BlacklistEntryListLimits.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = "createdAt",
            [FromQuery] string? sortOrder = "desc",
            CancellationToken cancellationToken = default)
        {
            NoStore();

            var list = BlacklistTextParser.ParseType(type);
            var field = BlacklistTextParser.ParseSortField(sortBy);
            var order = BlacklistTextParser.ParseSortOrder(sortOrder);

            if (list is null
                || page < 1
                || pageSize is < 1 or > BlacklistEntryListLimits.MaxPageSize
                || (search?.Length ?? 0) > BlacklistEntryListLimits.MaxSearchLength
                || field is null
                || order is null)
            {
                return ApiResults.ValidationFailed(
                    ListErrors(list, page, pageSize, search, field, order));
            }

            var entries = await _entries.GetPageAsync(
                list.Value,
                page,
                pageSize,
                search,
                field.Value,
                order.Value,
                cancellationToken);

            return Ok(new AdminBlacklistEntryPageResponse
            {
                Items = entries.Items.Select(ToResponse).ToList(),
                Total = entries.Total,
                Page = entries.Page,
                PageSize = entries.PageSize
            });
        }

        /// <summary>Reports one entry, for the edit dialog.</summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.ViewBlacklistsPolicy)]
        [ProducesResponseType<AdminBlacklistEntryResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEntry(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            NoStore();

            var entry = await _entries.GetAsync(id, cancellationToken);

            return entry is null
                ? Answer(new BlacklistEntryOutcome
                {
                    Action = BlacklistEntryAction.NotFound,
                    EntryId = id
                })
                : Ok(ToResponse(entry));
        }

        /// <summary>Creates an entry in one list.</summary>
        /// <remarks>
        /// The 201 carries a <c>Location</c>, and it is honest: the row exists in full the
        /// moment it is written and this very controller serves the address it names.
        /// </remarks>
        [HttpPost]
        [Authorize(Policy = AuthenticationExtensions.EditBlacklistsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateEntry(
            [FromBody] CreateBlacklistEntryRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var list = BlacklistTextParser.ParseType(request.Type);

            if (list is null)
            {
                return ApiResults.ValidationFailed(new Dictionary<string, string[]>(
                    StringComparer.Ordinal)
                {
                    ["type"] = [TypeHint]
                });
            }

            var outcome = await _entries.CreateAsync(
                new NewBlacklistEntry
                {
                    Type = list.Value,
                    Value = request.Value ?? string.Empty,
                    Note = request.Note
                },
                cancellationToken);

            var (statusCode, body) = BlacklistEntryResponses.Describe(outcome);

            if (statusCode == StatusCodes.Status201Created)
            {
                Response.Headers.Location = $"/admin/blacklist-entries/{outcome.EntryId}";
            }

            return StatusCode(statusCode, body);
        }

        /// <summary>
        /// Changes the value and the note of an entry. Every field the body can name is
        /// optional, and a body that names nothing is a success rather than a complaint - the
        /// convention every other PATCH on this API follows.
        /// </summary>
        [HttpPatch("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditBlacklistsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateEntry(
            [FromRoute] Guid id,
            [FromBody] UpdateBlacklistEntryRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await _entries.UpdateAsync(
                id,
                new BlacklistEntryPatch
                {
                    Value = request.Value,
                    Note = request.Note
                },
                cancellationToken));
        }

        /// <summary>Deletes an entry.</summary>
        /// <remarks>
        /// A <c>DELETE</c> that answers 200 rather than 204, because it has something to say:
        /// the difference between an entry that was deleted and one that was not there is the
        /// whole answer, and a visitor who deleted a row on another tab is told so.
        /// </remarks>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditBlacklistsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteEntry(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await _entries.DeleteAsync(id, cancellationToken));
        }

        /// <summary>
        /// Deletes every entry of one list at once: `DELETE /admin/blacklist-entries?type=…`.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The same verb on the collection rather than on a row. It names the list and nothing
        /// else: an applied search belongs to the view the operator is looking at, while "clear
        /// the list" is a statement about the list itself, so a clear narrowed by a filter would
        /// remove some rows and leave a screen that looks empty with rows still behind it. The
        /// type is therefore required and a missing one is refused rather than read as "all
        /// three" - the same rule the list read follows, for the same reason.
        /// </para>
        /// <para>
        /// An empty list is a success, not a refusal: the command is already satisfied, and
        /// answering otherwise would make the button report a failure for having worked.
        /// </para>
        /// </remarks>
        [HttpDelete]
        [Authorize(Policy = AuthenticationExtensions.EditBlacklistsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ClearEntries(
            [FromQuery] string? type = null,
            CancellationToken cancellationToken = default)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var list = BlacklistTextParser.ParseType(type);

            if (list is null)
            {
                return ApiResults.ValidationFailed(new Dictionary<string, string[]>(
                    StringComparer.Ordinal)
                {
                    ["type"] = [TypeHint]
                });
            }

            return Answer(await _entries.ClearAsync(list.Value, cancellationToken));
        }

        /// <summary>
        /// Which of the bounds a list request broke, keyed by the camelCase name of the query
        /// parameter so the reason lands under the input a client would fix.
        /// </summary>
        private static Dictionary<string, string[]> ListErrors(
            BlacklistType? type,
            int page,
            int pageSize,
            string? search,
            BlacklistSortField? field,
            BlacklistSortOrder? order)
        {
            var errors = ListQueryErrors.Common(
                page,
                pageSize,
                search,
                BlacklistEntryListLimits.MaxPageSize,
                BlacklistEntryListLimits.MaxSearchLength);

            if (type is null)
            {
                errors["type"] = [TypeHint];
            }

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
        /// where a <see cref="BlacklistEntryAction"/> becomes a number.
        /// </summary>
        private static IActionResult Answer(BlacklistEntryOutcome outcome)
        {
            var (status, body) = BlacklistEntryResponses.Describe(outcome);

            return new ObjectResult(body) { StatusCode = status };
        }

        private static AdminBlacklistEntryResponse ToResponse(BlacklistEntryView entry) => new()
        {
            Id = entry.Id.ToString(),
            Type = BlacklistTextParser.NameOf(entry.Type),
            Value = entry.Value,
            BlockedAttempts = entry.BlockedAttempts,
            Note = entry.Note,
            CreatedAt = entry.CreatedAt
        };
    }
}
