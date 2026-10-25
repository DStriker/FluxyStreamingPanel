using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Series;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Series.Commands;
using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Interfaces;
using Fluxy.Core.Series.Outcomes;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Content
{
    /// <summary>
    /// Creates a whole season of one series from a folder of video files.
    /// </summary>
    /// <remarks>
    /// A controller of its own rather than an action on <see cref="SeriesEpisodesController"/>:
    /// the request is a folder and two naming switches rather than one episode row, and the
    /// answer counts a batch. The folder is read on the server; a relative path is resolved
    /// against the content root the same way the on-demand player resolves a stored file.
    /// </remarks>
    [ApiController]
    [Route("admin/series/{seriesId:guid}/seasons")]
    [Authorize(Policy = AuthenticationExtensions.EditSeriesPolicy)]
    [Produces("application/json")]
    public sealed class SeriesSeasonsController : ApiControllerBase
    {
        /// <summary>Sentence a state that names no member gets.</summary>
        private const string StatusHint = "The status must be either active or inactive.";

        /// <summary>Sentence a delivery kind that names no member gets.</summary>
        private const string TypeHint = "The type must be either relay or redirect.";

        /// <summary>Sentence a season number that is not one gets.</summary>
        private const string SeasonHint =
            "The season number must be between 1 and 999.";

        private readonly ISeasonService _seasons;
        private readonly ISeriesService _series;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeriesSeasonsController"/> class.
        /// </summary>
        /// <param name="seasons">Service that reads the folder and stages the episodes.</param>
        /// <param name="series">Service used to refuse a request under a missing series.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public SeriesSeasonsController(
            ISeasonService seasons,
            ISeriesService series,
            IAntiforgery antiforgery,
            ILogger<SeriesSeasonsController> logger)
            : base(antiforgery, logger)
        {
            _seasons = seasons;
            _series = series;
        }

        /// <summary>Creates every episode of one season from the files in a folder.</summary>
        [HttpPost]
        [ProducesResponseType<SeasonCreatedResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateSeason(
            [FromRoute] Guid seriesId,
            [FromBody] CreateSeasonRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var seasonNumber = request.SeasonNumber ?? 0;
            if (seasonNumber is < 1 or > 999)
            {
                errors[nameof(CreateSeasonRequest.SeasonNumber)] = [SeasonHint];
            }

            var status = SeriesTextParser.ParseEpisodeStatusOrNull(request.Status);
            if (status is null)
            {
                if (string.IsNullOrWhiteSpace(request.Status))
                {
                    status = EpisodeStatus.Inactive;
                }
                else
                {
                    errors[nameof(CreateSeasonRequest.Status)] = [StatusHint];
                }
            }

            var type = SeriesTextParser.ParseEpisodeTypeOrNull(request.Type);
            if (type is null)
            {
                errors[nameof(CreateSeasonRequest.Type)] = [TypeHint];
            }

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            if (await _series.ExistsAsync(seriesId, cancellationToken) is false)
            {
                var (notFoundStatus, notFoundBody) = SeriesResponses.Describe(new SeasonOutcome
                {
                    Action = SeasonAction.NotFound,
                    SeriesId = seriesId,
                    SeasonNumber = seasonNumber,
                    AddedCount = 0
                });

                return new ObjectResult(notFoundBody) { StatusCode = notFoundStatus };
            }

            var outcome = await _seasons.CreateAsync(
                new NewSeason
                {
                    SeriesId = seriesId,
                    SeasonNumber = seasonNumber,
                    FolderPath = request.FolderPath?.Trim() ?? string.Empty,
                    AddSeasonNumberToName = request.AddSeasonNumberToName ?? false,
                    AddEpisodeNumberToName = request.AddEpisodeNumberToName ?? false,
                    Status = status!.Value,
                    Type = type!.Value
                },
                cancellationToken);

            if (outcome.Action is SeasonAction.Created)
            {
                Response.Headers.Location =
                    $"/admin/series/{seriesId}/episodes?season={seasonNumber}";

                return StatusCode(
                    StatusCodes.Status201Created,
                    SeriesResponses.CreatedSeason(outcome));
            }

            var (statusCode, body) = SeriesResponses.Describe(outcome);

            return StatusCode(statusCode, body);
        }
    }
}
