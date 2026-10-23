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
    /// The collective operations on the audio streams resource, split from
    /// <see cref="AudioStreamsController"/>.
    /// </summary>
    /// <remarks>
    /// The seam is the shape of the request and the answer, not the number of lines: a bulk
    /// request names a set of rows a table assembled on screen rather than one row with an
    /// address of its own, and it answers with a per-identifier report
    /// (<c>BulkOperationResponse</c>) where the rest of the resource answers with one outcome
    /// for one row. The route is <c>POST /admin/audio-streams/bulk</c>.
    ///
    /// The class policy is the permission the write beside it carries, because a bulk operation
    /// is one such write applied to several rows at once.
    /// </remarks>
    [ApiController]
    [Route("admin/audio-streams/bulk")]
    [Authorize(Policy = AuthenticationExtensions.EditAudioStreamsPolicy)]
    [Produces("application/json")]
    public sealed class AudioStreamsBulkController : ApiControllerBase
    {
        /// <summary>Names the operations a bulk body may ask for.</summary>
        private const string ActionHint =
            "The action must be 'start', 'stop', 'restart', 'delete', 'move-category', " +
            "'change-type' or 'change-restream'.";

        /// <summary>Sentence a category that is not an identifier gets.</summary>
        private const string CategoryHint =
            "The category must be the identifier of a category, as a uuid.";

        /// <summary>Sentence a destination stream kind this operation cannot use gets.</summary>
        private const string TypeHint =
            "The stream type must be one of: ondemand, redirect.";

        /// <summary>Sentence a set of identifiers that is not all uuids gets.</summary>
        private const string IdsHint = "Every identifier must be the uuid of a stream.";

        private readonly IAudioStreamBulkService _streams;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioStreamsBulkController"/> class.
        /// </summary>
        /// <param name="streams">Service that applies the operation to the named streams.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public AudioStreamsBulkController(
            IAudioStreamBulkService streams,
            IAntiforgery antiforgery,
            ILogger<AudioStreamsBulkController> logger)
            : base(antiforgery, logger)
        {
            _streams = streams;
        }

        /// <summary>
        /// Applies one operation to every audio stream the request names, and reports how each
        /// one ended.
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
        /// The menu is shorter than the video bulk endpoint's: an audio stream has no delay, no
        /// archive and no payload a kind of it can switch off, so there is nothing else to
        /// write collectively.
        /// </para>
        /// </remarks>
        [HttpPost]
        [ProducesResponseType<BulkOperationResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> BulkStreams(
            [FromBody] BulkAudioStreamsRequest request,
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
                errors[nameof(BulkAudioStreamsRequest.Action)] = [ActionHint];
            }

            var ids = ParseIds(request.Ids, errors);

            Guid? categoryId = null;

            if (action is BulkAudioStreamAction.MoveCategory)
            {
                if (Guid.TryParse(request.CategoryId, out var parsedCategory))
                {
                    categoryId = parsedCategory;
                }
                else
                {
                    errors[nameof(BulkAudioStreamsRequest.CategoryId)] = [CategoryHint];
                }
            }

            AudioStreamType? type = null;

            if (action is BulkAudioStreamAction.ChangeType)
            {
                type = AudioStreamTextParser.ParseTypeOrNull(request.Type);

                if (type is null)
                {
                    errors[nameof(BulkAudioStreamsRequest.Type)] = [TypeHint];
                }
            }

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            return Ok(BulkResponses.Describe(await _streams.BulkAsync(
                new BulkAudioStreamOperation
                {
                    Action = action!.Value,
                    StreamIds = ids!,
                    CategoryId = categoryId,
                    Type = type,
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
        private static BulkAudioStreamAction? ParseAction(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "start" => BulkAudioStreamAction.Start,
                "stop" => BulkAudioStreamAction.Stop,
                "restart" => BulkAudioStreamAction.Restart,
                "delete" => BulkAudioStreamAction.Delete,
                "move-category" => BulkAudioStreamAction.MoveCategory,
                "change-type" => BulkAudioStreamAction.ChangeType,
                "change-restream" => BulkAudioStreamAction.ChangeRestream,
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
                errors[nameof(BulkAudioStreamsRequest.Ids)] = ["Select at least one stream."];
                return null;
            }

            if (ids.Count > AudioStreamBulkLimits.MaxItems)
            {
                errors[nameof(BulkAudioStreamsRequest.Ids)] =
                [$"At most {AudioStreamBulkLimits.MaxItems} streams may be named in one request."];
                return null;
            }

            var parsed = new List<Guid>(ids.Count);

            foreach (var text in ids)
            {
                if (!Guid.TryParse(text, out var id))
                {
                    errors[nameof(BulkAudioStreamsRequest.Ids)] = [IdsHint];
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
