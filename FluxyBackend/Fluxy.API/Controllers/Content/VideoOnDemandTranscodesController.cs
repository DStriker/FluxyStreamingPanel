using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Vod;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Vod.Enums;
using Fluxy.Core.Vod.Interfaces;
using Fluxy.Core.Vod.Outcomes;
using Fluxy.Core.Vod.ReadModels;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Content
{
    /// <summary>
    /// The transcoding surface of the on-demand resource: which formats exist, and a request
    /// to convert one video.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A controller of its own because the question and the answer are about a conversion
    /// rather than about the video row: the list of formats is a fact about the installation,
    /// and the request is an operation on one video that is not a field of its edit form.
    /// </para>
    /// <para>
    /// <b>No encoder is configured in this pass.</b> The endpoints exist so the management
    /// screen can draw the operation and say why it is unavailable; a request answers
    /// <c>503 transcode_not_configured</c> and writes nothing. See
    /// <c>VodTranscodeService</c> for the reasoning.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/video-on-demand")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class VideoOnDemandTranscodesController : ApiControllerBase
    {
        /// <summary>Sentence a format that names no member gets.</summary>
        private const string FormatHint = "The format must be one of: mp4, avi, mkv, mpg, flv.";

        private readonly IVodService _items;
        private readonly IVodTranscodeService _transcodes;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoOnDemandTranscodesController"/> class.
        /// </summary>
        /// <param name="items">Service used to refuse a request for a missing video.</param>
        /// <param name="transcodes">Service that answers availability and the request.</param>
        /// <param name="antiforgery">Antiforgery service the base class checks with.</param>
        /// <param name="logger">Logger the base class reports through.</param>
        public VideoOnDemandTranscodesController(
            IVodService items,
            IVodTranscodeService transcodes,
            IAntiforgery antiforgery,
            ILogger<VideoOnDemandTranscodesController> logger)
            : base(antiforgery, logger)
        {
            _items = items;
            _transcodes = transcodes;
        }

        /// <summary>
        /// Reports whether this installation can transcode, and the formats the menu offers.
        /// </summary>
        /// <remarks>
        /// A fact about the server rather than about one video, so the table asks it once and
        /// can draw the action disabled with a reason instead of offering five buttons whose
        /// every press would be refused.
        /// </remarks>
        [HttpGet("transcode-formats")]
        [Authorize(Policy = AuthenticationExtensions.ViewVideoOnDemandPolicy)]
        [ProducesResponseType<AdminVodTranscodeAvailabilityResponse>(StatusCodes.Status200OK)]
        public IActionResult GetFormats()
            => Ok(ToResponse(_transcodes.GetAvailability()));

        /// <summary>Reports the same availability in the context of one existing video.</summary>
        /// <param name="id">Identifier of the video.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        [HttpGet("{id:guid}/transcodes")]
        [Authorize(Policy = AuthenticationExtensions.ViewVideoOnDemandPolicy)]
        [ProducesResponseType<AdminVodTranscodeAvailabilityResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTranscodes(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await _items.GetAsync(id, cancellationToken) is null)
            {
                return Answer(new VodOutcome { Action = VodAction.NotFound, VodId = id });
            }

            return Ok(ToResponse(_transcodes.GetAvailability()));
        }

        /// <summary>
        /// Asks for one video to be converted to one format.
        /// </summary>
        /// <returns>
        /// 503 <c>transcode_not_configured</c> while no encoder exists, 400
        /// <c>validation_failed</c> for a format that names no member, 404 for a missing video,
        /// and 400 <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// The video is checked before the format, so a request that names a video nobody has
        /// is answered about the video rather than about a field of a body that could not have
        /// changed anything anyway.
        /// </remarks>
        [HttpPost("{id:guid}/transcodes")]
        [Authorize(Policy = AuthenticationExtensions.EditVideoOnDemandPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status503ServiceUnavailable)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RequestTranscode(
            [FromRoute] Guid id,
            [FromBody] RequestVodTranscodeRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (await _items.GetAsync(id, cancellationToken) is null)
            {
                return Answer(new VodOutcome { Action = VodAction.NotFound, VodId = id });
            }

            var format = VodTextParser.ParseFormatOrNull(request.Format);

            if (format is null)
            {
                return ApiResults.ValidationFailed(
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        [nameof(RequestVodTranscodeRequest.Format)] = [FormatHint]
                    });
            }

            var (status, body) = VodResponses.Describe(_transcodes.Request(id, format.Value));

            return new ObjectResult(body) { StatusCode = status };
        }

        private static AdminVodTranscodeAvailabilityResponse ToResponse(
            VodTranscodeAvailability availability) => new()
            {
                Available = availability.Available,
                Formats = availability.Formats.Select(format => format.ToString()).ToList()
            };

        private static IActionResult Answer(VodOutcome outcome)
        {
            var (status, body) = VodResponses.Describe(outcome);

            return new ObjectResult(body) { StatusCode = status };
        }
    }
}
