using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Streams;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Interfaces;
using Fluxy.Core.Streams.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Content
{
    /// <summary>
    /// The collective operations on the streams resource, split from
    /// <see cref="VideoStreamsController"/>.
    /// </summary>
    /// <remarks>
    /// The seam is the shape of the request and the answer, not the number of lines: a bulk
    /// request names a set of rows a table assembled on screen rather than one row with an
    /// address of its own, and it answers with a per-identifier report
    /// (<c>BulkOperationResponse</c>) where the rest of the resource answers with one outcome
    /// for one row. The route is <c>POST /admin/video-streams/bulk</c>.
    ///
    /// The class policy is the permission the write beside it carries, because a bulk operation
    /// is one such write applied to several rows at once.
    /// </remarks>
    [ApiController]
    [Route("admin/video-streams/bulk")]
    [Authorize(Policy = AuthenticationExtensions.EditStreamsPolicy)]
    [Produces("application/json")]
    public sealed class VideoStreamsBulkController : ApiControllerBase
    {
        /// <summary>Names the operations a bulk body may ask for.</summary>
        private const string ActionHint =
            "The action must be 'start', 'stop', 'restart', 'delete', 'move-category', " +
            "'change-type', 'change-delay', 'change-archive' or 'change-restream'.";

        /// <summary>Sentence a category that is not an identifier gets.</summary>
        private const string CategoryHint =
            "The category must be the identifier of a category, as a uuid.";

        /// <summary>Sentence a destination stream kind this operation cannot use gets.</summary>
        private const string TypeHint =
            "The stream type must be one of: permanent, ondemand, redirect.";

        /// <summary>Sentence a delay with no usable value gets.</summary>
        private const string DelayHint = "The delay must be zero or more minutes.";

        /// <summary>Sentence an archive with no usable value gets.</summary>
        private const string ArchiveHint = "The archive length must be zero or more days.";

        /// <summary>Sentence a set of identifiers that is not all uuids gets.</summary>
        private const string IdsHint = "Every identifier must be the uuid of a stream.";

        private readonly IVideoStreamBulkService _streams;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoStreamsBulkController"/> class.
        /// </summary>
        /// <param name="streams">Service that applies the operation to the named streams.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public VideoStreamsBulkController(
            IVideoStreamBulkService streams,
            IAntiforgery antiforgery,
            ILogger<VideoStreamsBulkController> logger)
            : base(antiforgery, logger)
        {
            _streams = streams;
        }

        /// <summary>
        /// Applies one operation to every stream the request names, and reports how each one
        /// ended.
        /// </summary>
        /// <param name="request">Which operation, over which streams, and the value it writes.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 with one entry per identifier, 400 when the body broke a rule on itself (an
        /// unknown operation, no identifiers, more identifiers than one page holds, or a
        /// missing destination), 403 from the policy on the way in, and 400
        /// <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>A recorded departure from the REST shape of the rest of the resource</b>, the same
        /// one <c>UsersBulkController</c> records: the path carries a verb because the resource
        /// it would otherwise name does not exist. "These twenty streams" is a set the table on
        /// screen assembled in the browser, not a row with an address of its own.
        /// </para>
        /// <para>
        /// <b>The 200 describes the run, not every row in it.</b> What happened to a particular
        /// stream is stated in its own entry with the same code the one-row endpoint gives. A
        /// category move that collides with a name already used there refuses that row alone,
        /// and the others still move.
        /// </para>
        /// <para>
        /// No captcha and no attempt window, for the reason the rest of this area gives: both
        /// guard a request with no session at all, and everything here is behind an admin
        /// session, the edit permission and a fresh antiforgery pair. The identifiers are
        /// capped at the size of one page rather than accepted without bound.
        /// </para>
        /// </remarks>
        [HttpPost]
        [ProducesResponseType<BulkOperationResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> BulkStreams(
            [FromBody] BulkVideoStreamsRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            // Everything the body can be wrong about is read first, so one answer names every
            // mistake rather than one per round trip - and so that no row is touched by a
            // request that is about to be refused for another reason.
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var action = ParseAction(request.Action);
            if (action is null)
            {
                errors[nameof(BulkVideoStreamsRequest.Action)] = [ActionHint];
            }

            var ids = ParseIds(request.Ids, errors);

            Guid? categoryId = null;

            if (action is BulkVideoStreamAction.MoveCategory)
            {
                if (Guid.TryParse(request.CategoryId, out var parsedCategory))
                {
                    categoryId = parsedCategory;
                }
                else
                {
                    errors[nameof(BulkVideoStreamsRequest.CategoryId)] = [CategoryHint];
                }
            }

            VideoStreamType? type = null;

            if (action is BulkVideoStreamAction.ChangeType)
            {
                type = ParseTypeForBulk(request.Type);

                if (type is null)
                {
                    errors[nameof(BulkVideoStreamsRequest.Type)] = [TypeHint];
                }
            }

            if (action is BulkVideoStreamAction.ChangeDelay
                && request.DelayMinutes is not >= 0)
            {
                errors[nameof(BulkVideoStreamsRequest.DelayMinutes)] = [DelayHint];
            }

            if (action is BulkVideoStreamAction.ChangeArchive
                && request.ArchiveDays is not >= 0)
            {
                errors[nameof(BulkVideoStreamsRequest.ArchiveDays)] = [ArchiveHint];
            }

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            return Ok(BulkResponses.Describe(await _streams.BulkAsync(
                new BulkVideoStreamOperation
                {
                    Action = action!.Value,
                    StreamIds = ids!,
                    CategoryId = categoryId,
                    Type = type,
                    DelayMinutes = request.DelayMinutes,
                    ArchiveDays = request.ArchiveDays,
                    RestreamUserAgent = request.RestreamUserAgent,
                    RestreamProxy = request.RestreamProxy,
                    RestreamCookies = request.RestreamCookies,
                    RestreamHeaders = request.RestreamHeaders
                },
                cancellationToken)));
        }

        /// <summary>
        /// Turns the text a bulk body sent into the operation it names, or nothing at all.
        /// </summary>
        /// <remarks>
        /// Compared after lowering, with hyphens in the spelling the rest of this API uses, so
        /// <c>Start</c> and <c>change-type</c> are both accepted. Enum binding is deliberately
        /// not used, for the reason the accounts bulk endpoint gives: it would refuse the
        /// conventional lower-case form.
        /// </remarks>
        private static BulkVideoStreamAction? ParseAction(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "start" => BulkVideoStreamAction.Start,
                "stop" => BulkVideoStreamAction.Stop,
                "restart" => BulkVideoStreamAction.Restart,
                "delete" => BulkVideoStreamAction.Delete,
                "move-category" => BulkVideoStreamAction.MoveCategory,
                "change-type" => BulkVideoStreamAction.ChangeType,
                "change-delay" => BulkVideoStreamAction.ChangeDelay,
                "change-archive" => BulkVideoStreamAction.ChangeArchive,
                "change-restream" => BulkVideoStreamAction.ChangeRestream,
                _ => null
            };

        /// <summary>
        /// Turns the text of a destination kind into the member it names, within the three
        /// kinds a bulk change can safely write.
        /// </summary>
        private static VideoStreamType? ParseTypeForBulk(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "permanent" => VideoStreamType.Permanent,
                "ondemand" or "on-demand" => VideoStreamType.OnDemand,
                "redirect" => VideoStreamType.Redirect,
                _ => null
            };

        /// <summary>
        /// Turns the identifiers a bulk body sent into the rows they name, or nothing at all
        /// once one of them is wrong.
        /// </summary>
        /// <remarks>
        /// An empty list is refused rather than answered with an empty run - an operation over
        /// zero streams is a success that changed nothing, and a client that meant to send
        /// twenty deserves to be told its body did not arrive. Duplicates collapse rather than
        /// being refused: a page cannot hold the same row twice, so a duplicate can only come
        /// from a client assembling the list by hand.
        /// </remarks>
        private static IReadOnlyList<Guid>? ParseIds(
            IReadOnlyList<string>? ids,
            Dictionary<string, string[]> errors)
        {
            if (ids is null || ids.Count is 0)
            {
                errors[nameof(BulkVideoStreamsRequest.Ids)] = ["Select at least one stream."];
                return null;
            }

            if (ids.Count > VideoStreamBulkLimits.MaxItems)
            {
                errors[nameof(BulkVideoStreamsRequest.Ids)] =
                [$"At most {VideoStreamBulkLimits.MaxItems} streams may be named in one request."];
                return null;
            }

            var parsed = new List<Guid>(ids.Count);

            foreach (var text in ids)
            {
                if (!Guid.TryParse(text, out var id))
                {
                    errors[nameof(BulkVideoStreamsRequest.Ids)] = [IdsHint];
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
