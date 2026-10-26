using System.Globalization;
using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Epg;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Epg.Commands;
using Fluxy.Core.Epg.Enums;
using Fluxy.Core.Epg.Interfaces;
using Fluxy.Core.Epg.Outcomes;
using Fluxy.Core.Epg.ReadModels;
using Fluxy.Core.Epg.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Content
{
    /// <summary>
    /// Managing the programme guide sources: one page of the table, one source in full, the
    /// writes that create, change and delete them, and the read that answers which streams use
    /// a source.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two policies on every action: the class carries <c>AdminOnly</c> the way the other
    /// content controllers do, and each action adds its own permission on top. Read and write
    /// are separate grants, so a group which may look at the guides and a group which may point
    /// streams at them stay two different things.
    /// </para>
    /// <para>
    /// <b>No captcha and no attempt window</b>, for the reason the rest of the admin area
    /// gives: both guard a request with no session at all, and everything here is behind an
    /// admin session and a fresh antiforgery pair.
    /// </para>
    /// <para>
    /// <b>The channels endpoint is the only derived read.</b> Nothing about it is stored: a
    /// stream is a channel of a source when it names the source and a key, so the table is a
    /// projection of the streams table rather than a second list that could disagree with it.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/epg-sources")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class EpgSourcesController : ApiControllerBase
    {
        /// <summary>Sentence a column that names nothing gets.</summary>
        private const string SortByHint =
            "The sort field must be one of: name, url, dayskeep, lastepgupdatedat.";

        /// <summary>Sentence a channel column that names nothing gets.</summary>
        private const string ChannelSortByHint =
            "The sort field must be one of: streamname, channelkey.";

        /// <summary>Sentence a direction that names nothing gets.</summary>
        private const string SortOrderHint = "The sort order must be either asc or desc.";

        /// <summary>Sentence a retention that is not a number gets.</summary>
        private const string DaysKeepHint = "The number of days to keep must be a whole number.";

        private readonly IEpgSourceService _sources;

        /// <summary>
        /// Initializes a new instance of the <see cref="EpgSourcesController"/> class.
        /// </summary>
        /// <param name="sources">Service that reads and changes the sources.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public EpgSourcesController(
            IEpgSourceService sources,
            IAntiforgery antiforgery,
            ILogger<EpgSourcesController> logger)
            : base(antiforgery, logger)
        {
            _sources = sources;
        }

        /// <summary>
        /// Reports one page of the table, filtered and ordered the way the query asked.
        /// </summary>
        /// <remarks>
        /// Search, order and paging live here rather than in the browser because the pager
        /// shows a <c>total</c>. The bounds are refused rather than clamped, and a value that
        /// names nothing is refused rather than treated as "no filter".
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewEpgSourcesPolicy)]
        [ProducesResponseType<AdminEpgSourcePageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSources(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = EpgSourceListLimits.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = "name",
            [FromQuery] string? sortOrder = "asc",
            CancellationToken cancellationToken = default)
        {
            NoStore();

            var field = EpgSourceTextParser.ParseSortField(sortBy);
            var order = EpgSourceTextParser.ParseSortOrder(sortOrder);

            if (page < 1
                || pageSize is < 1 or > EpgSourceListLimits.MaxPageSize
                || (search?.Length ?? 0) > EpgSourceListLimits.MaxSearchLength
                || field is null
                || order is null)
            {
                return ApiResults.ValidationFailed(ListErrors(page, pageSize, search, field, order));
            }

            var list = await _sources.GetPageAsync(
                page,
                pageSize,
                search,
                field.Value,
                order.Value,
                cancellationToken);

            return Ok(new AdminEpgSourcePageResponse
            {
                Items = list.Items.Select(ToResponse).ToList(),
                Total = list.Total,
                Page = list.Page,
                PageSize = list.PageSize
            });
        }

        /// <summary>Reports one source, for the edit form.</summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.ViewEpgSourcesPolicy)]
        [ProducesResponseType<AdminEpgSourceResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSource(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            NoStore();

            var source = await _sources.GetAsync(id, cancellationToken);

            return source is null
                ? Answer(new EpgSourceOutcome { Action = EpgSourceAction.NotFound, SourceId = id })
                : Ok(ToResponse(source));
        }

        /// <summary>
        /// Reports the streams that read their guide from a source, one page at a time.
        /// </summary>
        /// <remarks>
        /// A source that does not exist and one with no channels are two different answers:
        /// the first is a 404, the second a legitimate page of nothing. Both are reported here
        /// rather than by an empty list, because a form that mistyped the identifier must not
        /// be told that the source simply has no channels.
        /// </remarks>
        [HttpGet("{id:guid}/channels")]
        [Authorize(Policy = AuthenticationExtensions.ViewEpgSourcesPolicy)]
        [ProducesResponseType<AdminEpgSourceChannelPageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetChannels(
            [FromRoute] Guid id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = EpgSourceListLimits.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = "streamname",
            [FromQuery] string? sortOrder = "asc",
            CancellationToken cancellationToken = default)
        {
            NoStore();

            var field = EpgSourceTextParser.ParseChannelSortField(sortBy);
            var order = EpgSourceTextParser.ParseChannelSortOrder(sortOrder);

            if (page < 1
                || pageSize is < 1 or > EpgSourceListLimits.MaxPageSize
                || (search?.Length ?? 0) > EpgSourceListLimits.MaxSearchLength
                || field is null
                || order is null)
            {
                return ApiResults.ValidationFailed(
                    ChannelListErrors(page, pageSize, search, field, order));
            }

            var list = await _sources.GetChannelsAsync(
                id,
                page,
                pageSize,
                search,
                field.Value,
                order.Value,
                cancellationToken);

            if (list is null)
            {
                return Answer(new EpgSourceOutcome
                {
                    Action = EpgSourceAction.NotFound,
                    SourceId = id
                });
            }

            return Ok(new AdminEpgSourceChannelPageResponse
            {
                Items = list.Items.Select(ToChannelResponse).ToList(),
                Total = list.Total,
                Page = list.Page,
                PageSize = list.PageSize
            });
        }

        /// <summary>Creates a source.</summary>
        /// <remarks>
        /// The 201 carries a <c>Location</c>, and it is honest: the row exists in full the
        /// moment it is written and this very controller serves the address it names.
        /// </remarks>
        [HttpPost]
        [Authorize(Policy = AuthenticationExtensions.EditEpgSourcesPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateSource(
            [FromBody] CreateEpgSourceRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var daysKeep = ParseDaysKeep(request.DaysKeep, errors);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _sources.CreateAsync(
                new NewEpgSource
                {
                    Name = request.Name?.Trim() ?? string.Empty,
                    Url = request.Url?.Trim() ?? string.Empty,
                    DaysKeep = daysKeep
                },
                cancellationToken);

            var (statusCode, body) = EpgSourceResponses.Describe(outcome);

            if (statusCode == StatusCodes.Status201Created)
            {
                Response.Headers.Location = $"/admin/epg-sources/{outcome.SourceId}";
            }

            return StatusCode(statusCode, body);
        }

        /// <summary>Changes a source. Every field the body can name is optional.</summary>
        /// <remarks>
        /// A body that names nothing is a success rather than a complaint, the convention every
        /// other PATCH on this API follows.
        /// </remarks>
        [HttpPatch("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditEpgSourcesPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateSource(
            [FromRoute] Guid id,
            [FromBody] UpdateEpgSourceRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var daysKeep = ParseDaysKeep(request.DaysKeep, errors);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _sources.UpdateAsync(
                id,
                new EpgSourcePatch
                {
                    Name = request.Name,
                    Url = request.Url,
                    DaysKeep = daysKeep
                },
                cancellationToken);

            return Answer(outcome);
        }

        /// <summary>Deletes a source.</summary>
        /// <remarks>
        /// A <c>DELETE</c> that answers 200 rather than 204, because it has something to say:
        /// the difference between a source that was deleted, one that was not there and one a
        /// stream still depends on is the whole answer.
        /// </remarks>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditEpgSourcesPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteSource(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await _sources.DeleteAsync(id, cancellationToken));
        }

        /// <summary>
        /// Reads the optional retention of a create or a change. A blank value means "not
        /// named": the create falls back to the default and the patch keeps what the row holds.
        /// </summary>
        private static int? ParseDaysKeep(
            string? value,
            Dictionary<string, string[]> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (int.TryParse(
                value.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed))
            {
                return parsed;
            }

            errors["daysKeep"] = [DaysKeepHint];

            return null;
        }

        /// <summary>
        /// Which of the bounds a source list request broke, keyed by the camelCase name of the
        /// query parameter so the reason lands under the input a client would fix.
        /// </summary>
        private static Dictionary<string, string[]> ListErrors(
            int page,
            int pageSize,
            string? search,
            EpgSourceSortField? field,
            EpgSourceSortOrder? order)
        {
            var errors = ListQueryErrors.Common(
                page,
                pageSize,
                search,
                EpgSourceListLimits.MaxPageSize,
                EpgSourceListLimits.MaxSearchLength);

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

        /// <summary>Which of the bounds a channels request broke.</summary>
        private static Dictionary<string, string[]> ChannelListErrors(
            int page,
            int pageSize,
            string? search,
            EpgSourceChannelSortField? field,
            EpgSourceChannelSortOrder? order)
        {
            var errors = ListQueryErrors.Common(
                page,
                pageSize,
                search,
                EpgSourceListLimits.MaxPageSize,
                EpgSourceListLimits.MaxSearchLength);

            if (field is null)
            {
                errors["sortBy"] = [ChannelSortByHint];
            }

            if (order is null)
            {
                errors["sortOrder"] = [SortOrderHint];
            }

            return errors;
        }

        /// <summary>
        /// Turns the service's answer into the status and body a client is told, the one place
        /// where an <see cref="EpgSourceAction"/> becomes a number.
        /// </summary>
        private static IActionResult Answer(EpgSourceOutcome outcome)
        {
            var (status, body) = EpgSourceResponses.Describe(outcome);

            return new ObjectResult(body) { StatusCode = status };
        }

        private static AdminEpgSourceResponse ToResponse(EpgSourceView source) => new()
        {
            Id = source.Id.ToString(),
            Name = source.Name,
            Url = source.Url,
            DaysKeep = source.DaysKeep,
            LastEpgUpdatedAt = source.LastEpgUpdatedAt
        };

        private static AdminEpgSourceChannelResponse ToChannelResponse(EpgSourceChannel channel) => new()
        {
            StreamId = channel.StreamId.ToString(),
            StreamName = channel.StreamName,
            EpgChannelKey = channel.EpgChannelKey
        };
    }
}
