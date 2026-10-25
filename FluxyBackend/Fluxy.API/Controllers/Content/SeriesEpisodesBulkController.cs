using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Series;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Series.Commands;
using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Interfaces;
using Fluxy.Core.Series.Outcomes;
using Fluxy.Core.Series.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Content
{
    /// <summary>
    /// The collective operations on the episodes of one series, split from
    /// <see cref="SeriesEpisodesController"/>.
    /// </summary>
    [ApiController]
    [Route("admin/series/{seriesId:guid}/episodes/bulk")]
    [Authorize(Policy = AuthenticationExtensions.EditSeriesPolicy)]
    [Produces("application/json")]
    public sealed class SeriesEpisodesBulkController : ApiControllerBase
    {
        /// <summary>Names the operations a bulk body may ask for.</summary>
        private const string ActionHint =
            "The action must be 'change-status', 'change-type' or 'delete'.";

        /// <summary>Sentence a state that names no member gets.</summary>
        private const string StatusHint = "The status must be either active or inactive.";

        /// <summary>Sentence a delivery kind that names no member gets.</summary>
        private const string TypeHint = "The type must be either relay or redirect.";

        /// <summary>Sentence a set of identifiers that is not all uuids gets.</summary>
        private const string IdsHint = "Every identifier must be the uuid of an episode.";

        private readonly IEpisodeBulkService _bulk;
        private readonly ISeriesService _series;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeriesEpisodesBulkController"/> class.
        /// </summary>
        /// <param name="bulk">Service that applies the operation to the named episodes.</param>
        /// <param name="series">Service used to refuse a request under a missing series.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public SeriesEpisodesBulkController(
            IEpisodeBulkService bulk,
            ISeriesService series,
            IAntiforgery antiforgery,
            ILogger<SeriesEpisodesBulkController> logger)
            : base(antiforgery, logger)
        {
            _bulk = bulk;
            _series = series;
        }

        /// <summary>Applies one operation to every episode the request names.</summary>
        [HttpPost]
        [ProducesResponseType<BulkOperationResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> BulkEpisodes(
            [FromRoute] Guid seriesId,
            [FromBody] BulkEpisodeRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (await _series.ExistsAsync(seriesId, cancellationToken) is false)
            {
                var (notFoundStatus, notFoundBody) = SeriesResponses.Describe(new SeriesOutcome
                {
                    Action = SeriesAction.NotFound,
                    SeriesId = seriesId
                });

                return new ObjectResult(notFoundBody) { StatusCode = notFoundStatus };
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var action = SeriesTextParser.ParseBulkEpisodeActionOrNull(request.Action);
            if (action is null)
            {
                errors[nameof(BulkEpisodeRequest.Action)] = [ActionHint];
            }

            var ids = ParseIds(request.Ids, errors);

            EpisodeStatus? status = null;
            if (action is BulkEpisodeAction.ChangeStatus)
            {
                status = SeriesTextParser.ParseEpisodeStatusOrNull(request.Status);
                if (status is null)
                {
                    errors[nameof(BulkEpisodeRequest.Status)] = [StatusHint];
                }
            }

            EpisodeType? type = null;
            if (action is BulkEpisodeAction.ChangeType)
            {
                type = SeriesTextParser.ParseEpisodeTypeOrNull(request.Type);
                if (type is null)
                {
                    errors[nameof(BulkEpisodeRequest.Type)] = [TypeHint];
                }
            }

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            return Ok(BulkResponses.Describe(await _bulk.BulkAsync(
                seriesId,
                new BulkEpisodeOperation
                {
                    Action = action!.Value,
                    EpisodeIds = ids!,
                    Status = status,
                    Type = type
                },
                cancellationToken)));
        }

        private static IReadOnlyList<Guid>? ParseIds(
            IReadOnlyList<string>? ids,
            Dictionary<string, string[]> errors)
        {
            if (ids is null || ids.Count is 0)
            {
                errors[nameof(BulkEpisodeRequest.Ids)] = ["Select at least one episode."];
                return null;
            }

            if (ids.Count > EpisodeBulkLimits.MaxItems)
            {
                errors[nameof(BulkEpisodeRequest.Ids)] =
                [$"At most {EpisodeBulkLimits.MaxItems} episodes may be named in one request."];
                return null;
            }

            var parsed = new List<Guid>(ids.Count);

            foreach (var text in ids)
            {
                if (!Guid.TryParse(text, out var id))
                {
                    errors[nameof(BulkEpisodeRequest.Ids)] = [IdsHint];
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
