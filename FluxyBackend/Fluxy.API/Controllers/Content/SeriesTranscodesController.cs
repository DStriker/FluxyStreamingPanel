using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Series;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Interfaces;
using Fluxy.Core.Series.Outcomes;
using Fluxy.Core.Series.ReadModels;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Content
{
    /// <summary>
    /// The transcoding surface of the series resource: which formats exist, and a request to
    /// convert one episode.
    /// </summary>
    /// <remarks>
    /// A controller of its own because the question and the answer are about a conversion
    /// rather than about a row: the list of formats is a fact about the installation, and the
    /// request is an operation on one episode that is not a field of its edit form. No encoder
    /// is configured in this pass, so the endpoints exist to draw the operation and say why it
    /// is unavailable; a request answers <c>503 transcode_not_configured</c> and writes nothing.
    /// </remarks>
    [ApiController]
    [Route("admin/series")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class SeriesTranscodesController : ApiControllerBase
    {
        /// <summary>Sentence a format that names no member gets.</summary>
        private const string FormatHint = "The format must be one of: mp4, avi, mkv, mpg, flv.";

        private readonly IEpisodeService _episodes;
        private readonly ISeriesService _series;
        private readonly IEpisodeTranscodeService _transcodes;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeriesTranscodesController"/> class.
        /// </summary>
        /// <param name="episodes">Service used to refuse a request for a missing episode.</param>
        /// <param name="series">Service used to refuse a request for a missing series.</param>
        /// <param name="transcodes">Service that answers availability and the request.</param>
        /// <param name="antiforgery">Antiforgery service the base class checks with.</param>
        /// <param name="logger">Logger the base class reports through.</param>
        public SeriesTranscodesController(
            IEpisodeService episodes,
            ISeriesService series,
            IEpisodeTranscodeService transcodes,
            IAntiforgery antiforgery,
            ILogger<SeriesTranscodesController> logger)
            : base(antiforgery, logger)
        {
            _episodes = episodes;
            _series = series;
            _transcodes = transcodes;
        }

        /// <summary>
        /// Reports whether this installation can transcode, and the formats the menu offers.
        /// </summary>
        [HttpGet("transcode-formats")]
        [Authorize(Policy = AuthenticationExtensions.ViewSeriesPolicy)]
        [ProducesResponseType<AdminEpisodeTranscodeAvailabilityResponse>(StatusCodes.Status200OK)]
        public IActionResult GetFormats()
        {
            NoStore();

            return Ok(ToResponse(_transcodes.GetAvailability()));
        }

        /// <summary>Reports the same availability in the context of one existing episode.</summary>
        [HttpGet("{seriesId:guid}/episodes/{episodeId:guid}/transcodes")]
        [Authorize(Policy = AuthenticationExtensions.ViewSeriesPolicy)]
        [ProducesResponseType<AdminEpisodeTranscodeAvailabilityResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTranscodes(
            [FromRoute] Guid seriesId,
            [FromRoute] Guid episodeId,
            CancellationToken cancellationToken)
        {
            NoStore();

            if (await MissingTargetAsync(seriesId, episodeId, cancellationToken) is { } missing)
            {
                return missing;
            }

            return Ok(ToResponse(_transcodes.GetAvailability()));
        }

        /// <summary>Asks for one episode to be converted to one format.</summary>
        [HttpPost("{seriesId:guid}/episodes/{episodeId:guid}/transcodes")]
        [Authorize(Policy = AuthenticationExtensions.EditSeriesPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status503ServiceUnavailable)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RequestTranscode(
            [FromRoute] Guid seriesId,
            [FromRoute] Guid episodeId,
            [FromBody] RequestEpisodeTranscodeRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (await MissingTargetAsync(seriesId, episodeId, cancellationToken) is { } missing)
            {
                return missing;
            }

            var format = SeriesTextParser.ParseFormatOrNull(request.Format);

            if (format is null)
            {
                return ApiResults.ValidationFailed(
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        [nameof(RequestEpisodeTranscodeRequest.Format)] = [FormatHint]
                    });
            }

            var (status, body) = SeriesResponses.Describe(
                _transcodes.Request(seriesId, episodeId, format.Value));

            return new ObjectResult(body) { StatusCode = status };
        }

        private async Task<IActionResult?> MissingTargetAsync(
            Guid seriesId,
            Guid episodeId,
            CancellationToken cancellationToken)
        {
            if (await _series.ExistsAsync(seriesId, cancellationToken) is false)
            {
                var (status, body) = SeriesResponses.Describe(new SeriesOutcome
                {
                    Action = SeriesAction.NotFound,
                    SeriesId = seriesId
                });

                return new ObjectResult(body) { StatusCode = status };
            }

            if (await _episodes.GetAsync(seriesId, episodeId, cancellationToken) is null)
            {
                var (status, body) = SeriesResponses.Describe(new EpisodeOutcome
                {
                    Action = EpisodeAction.NotFound,
                    SeriesId = seriesId,
                    EpisodeId = episodeId
                });

                return new ObjectResult(body) { StatusCode = status };
            }

            return null;
        }

        private static AdminEpisodeTranscodeAvailabilityResponse ToResponse(
            EpisodeTranscodeAvailability availability) => new()
            {
                Available = availability.Available,
                Formats = availability.Formats
                    .Select(format => format.ToString())
                    .ToList()
            };
    }
}
