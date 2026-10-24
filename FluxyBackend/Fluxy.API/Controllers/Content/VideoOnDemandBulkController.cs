using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Vod;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Vod.Commands;
using Fluxy.Core.Vod.Enums;
using Fluxy.Core.Vod.Interfaces;
using Fluxy.Core.Vod.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Content
{
    /// <summary>
    /// The collective operations on the on-demand resource, split from
    /// <see cref="VideoOnDemandController"/>.
    /// </summary>
    /// <remarks>
    /// The seam is the shape of the request and the answer: a bulk request names a set of rows
    /// a table assembled on screen rather than one row with an address of its own, and it
    /// answers with a per-identifier report where the rest of the resource answers with one
    /// outcome for one row. The route is <c>POST /admin/video-on-demand/bulk</c>.
    /// </remarks>
    [ApiController]
    [Route("admin/video-on-demand/bulk")]
    [Authorize(Policy = AuthenticationExtensions.EditVideoOnDemandPolicy)]
    [Produces("application/json")]
    public sealed class VideoOnDemandBulkController : ApiControllerBase
    {
        /// <summary>Names the operations a bulk body may ask for.</summary>
        private const string ActionHint =
            "The action must be 'change-status', 'move-category', 'change-type' or 'delete'.";

        /// <summary>Sentence a category that is not an identifier gets.</summary>
        private const string CategoryHint =
            "The category must be the identifier of a category, as a uuid.";

        /// <summary>Sentence a state that names no member gets.</summary>
        private const string StatusHint = "The status must be either active or inactive.";

        /// <summary>Sentence a kind that names no member gets.</summary>
        private const string TypeHint = "The type must be either relay or redirect.";

        /// <summary>Sentence a set of identifiers that is not all uuids gets.</summary>
        private const string IdsHint = "Every identifier must be the uuid of a video.";

        private readonly IVodBulkService _bulk;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoOnDemandBulkController"/> class.
        /// </summary>
        /// <param name="bulk">Service that applies the operation to the named videos.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public VideoOnDemandBulkController(
            IVodBulkService bulk,
            IAntiforgery antiforgery,
            ILogger<VideoOnDemandBulkController> logger)
            : base(antiforgery, logger)
        {
            _bulk = bulk;
        }

        /// <summary>
        /// Applies one operation to every video the request names, and reports how each ended.
        /// </summary>
        /// <remarks>
        /// Everything the body can be wrong about is read first, so one answer names every
        /// mistake rather than one per round trip - and so that no row is touched by a request
        /// that is about to be refused for another reason. No captcha and no attempt window,
        /// for the reason the rest of the admin area gives.
        /// </remarks>
        [HttpPost]
        [ProducesResponseType<BulkOperationResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> BulkVideos(
            [FromBody] BulkVodRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var action = VodTextParser.ParseBulkActionOrNull(request.Action);
            if (action is null)
            {
                errors[nameof(BulkVodRequest.Action)] = [ActionHint];
            }

            var ids = ParseIds(request.Ids, errors);

            Guid? categoryId = null;
            if (action is BulkVodAction.MoveCategory)
            {
                if (Guid.TryParse(request.CategoryId, out var parsedCategory))
                {
                    categoryId = parsedCategory;
                }
                else
                {
                    errors[nameof(BulkVodRequest.CategoryId)] = [CategoryHint];
                }
            }

            VodStatus? status = null;
            if (action is BulkVodAction.ChangeStatus)
            {
                status = VodTextParser.ParseStatusOrNull(request.Status);
                if (status is null)
                {
                    errors[nameof(BulkVodRequest.Status)] = [StatusHint];
                }
            }

            VodType? type = null;
            if (action is BulkVodAction.ChangeType)
            {
                type = VodTextParser.ParseTypeOrNull(request.Type);
                if (type is null)
                {
                    errors[nameof(BulkVodRequest.Type)] = [TypeHint];
                }
            }

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            return Ok(BulkResponses.Describe(await _bulk.BulkAsync(
                new BulkVodOperation
                {
                    Action = action!.Value,
                    VodIds = ids!,
                    CategoryId = categoryId,
                    Status = status,
                    Type = type
                },
                cancellationToken)));
        }

        /// <summary>
        /// Turns the identifiers the body sent into the rows they name.
        /// </summary>
        /// <remarks>
        /// An empty list is refused rather than answered with an empty run. Duplicates collapse
        /// rather than being refused: a page cannot hold the same row twice.
        /// </remarks>
        private static IReadOnlyList<Guid>? ParseIds(
            IReadOnlyList<string>? ids,
            Dictionary<string, string[]> errors)
        {
            if (ids is null || ids.Count is 0)
            {
                errors[nameof(BulkVodRequest.Ids)] = ["Select at least one video."];
                return null;
            }

            if (ids.Count > VodBulkLimits.MaxItems)
            {
                errors[nameof(BulkVodRequest.Ids)] =
                [$"At most {VodBulkLimits.MaxItems} videos may be named in one request."];
                return null;
            }

            var parsed = new List<Guid>(ids.Count);

            foreach (var text in ids)
            {
                if (!Guid.TryParse(text, out var id))
                {
                    errors[nameof(BulkVodRequest.Ids)] = [IdsHint];
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
