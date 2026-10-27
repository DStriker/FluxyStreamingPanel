using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Content;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Content.Commands;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Interfaces;
using Fluxy.Core.Content.ReadModels;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Content
{
    /// <summary>
    /// Managing the bouquets - the named packages of content - and the content each one holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One policy on the class (<c>AdminOnly</c>) with the two bouquet permissions on top of it,
    /// one per action, the shape the streams controller uses: every route here is an admin
    /// route, and a read is <c>viewBouquets</c> while a write is <c>editBouquets</c>. The
    /// difference matters because managing packages is a job that can be delegated apart from
    /// managing the content inside them.
    /// </para>
    /// <para>
    /// <b>No captcha and no attempt window</b>, for the reason the accounts controller gives:
    /// both exist to guard a request with no session at all, and everything here is behind an
    /// admin session and a fresh antiforgery pair.
    /// </para>
    /// <para>
    /// <b>The list is not paged, deliberately</b>, for the reason the categories list gives: the
    /// bouquets are a screenful of packages by construction, and a pager over a list that never
    /// outgrows one page would promise a second page that has no reason to exist.
    /// </para>
    /// <para>
    /// <b>An order is a whole-list write, twice.</b> The bouquets themselves are re-arranged by
    /// <c>PUT /admin/bouquets/order</c>, and the content of one kind by
    /// <c>PUT /admin/bouquets/{id}/content</c>. Both replace the order they own in one save:
    /// a per-row position patch plus shifting the neighbours would be several writes where one
    /// atomic one exists, and the caller - a screen that has just shown a drag over the entire
    /// list - already knows all of it.
    /// </para>
    /// <para>
    /// Both reads carry <c>Cache-Control: no-store</c> through
    /// <see cref="ApiControllerBase.NoStore"/>: this is the structure of a private area, and a
    /// shared cache that kept one would hand it to the next visitor.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/bouquets")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class BouquetsController : ApiControllerBase
    {
        /// <summary>Sentence a type that names no known member gets.</summary>
        private const string TypeHint =
            "The content type must be one of: videostream, audiostream, videoondemand, series.";

        /// <summary>Sentence a name that is blank or too long gets.</summary>
        private const string NameHint =
            "The name must be between 1 and 100 characters.";

        /// <summary>Sentence a set of bouquet identifiers that is not all uuids gets.</summary>
        private const string BouquetIdsHint = "Every identifier must be the uuid of a bouquet.";

        /// <summary>Sentence a set of content identifiers that is not all uuids gets.</summary>
        private const string ContentIdsHint = "Every identifier must be the uuid of a content item.";

        private readonly IBouquetService _bouquets;

        /// <summary>
        /// Initializes a new instance of the <see cref="BouquetsController"/> class.
        /// </summary>
        /// <param name="bouquets">Service that reads and changes the bouquets.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public BouquetsController(
            IBouquetService bouquets,
            IAntiforgery antiforgery,
            ILogger<BouquetsController> logger)
            : base(antiforgery, logger)
        {
            _bouquets = bouquets;
        }

        /// <summary>
        /// Reports every bouquet, with the counts of what each one holds, in the order an
        /// operator arranged them in.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with the whole list.</returns>
        /// <remarks>
        /// No antiforgery pair: the method changes nothing. No filter and no pager either - the
        /// list is the bouquets as they are, which is exactly what the table draws and what the
        /// bouquet picker on the four content forms needs.
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewBouquetsPolicy)]
        [ProducesResponseType<AdminBouquetListResponse>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetBouquets(CancellationToken cancellationToken)
        {
            NoStore();

            var items = await _bouquets.GetAllAsync(cancellationToken);

            return Ok(new AdminBouquetListResponse
            {
                Items = items.Select(ToItem).ToList()
            });
        }

        /// <summary>
        /// Creates a bouquet at the end of the list.
        /// </summary>
        /// <param name="request">The bouquet to create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 201 with a <c>Location</c> naming the new bouquet, 400 with the rejected fields, 409
        /// when another bouquet holds that name in any spelling, 400 <c>csrf_invalid</c> without
        /// the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// The 201 carries a <c>Location</c>, and it is honest: the row exists in full the
        /// moment it is written, and this very controller serves the list it appears in. There
        /// is deliberately no <c>sortOrder</c> in the body - a new bouquet joins the end, and
        /// where the end is belongs to the database rather than to a caller one stale screen
        /// behind.
        /// </remarks>
        [HttpPost]
        [Authorize(Policy = AuthenticationExtensions.EditBouquetsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateBouquet(
            [FromBody] CreateBouquetRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return ApiResults.ValidationFailed(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [nameof(CreateBouquetRequest.Name)] = [NameHint]
                });
            }

            var outcome = await _bouquets.CreateAsync(
                new NewBouquet { Name = request.Name.Trim() },
                cancellationToken);

            var (statusCode, body) = BouquetResponses.Describe(outcome);

            if (statusCode == StatusCodes.Status201Created)
            {
                Response.Headers.Location = $"/admin/bouquets/{outcome.BouquetId}";
            }

            return StatusCode(statusCode, body);
        }

        /// <summary>
        /// Renames one bouquet.
        /// </summary>
        /// <param name="id">Identifier of the bouquet to change, in the path.</param>
        /// <param name="request">The fields to change. An absent field is left alone.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 when it was written, 400 with the rejected fields, 404 when there is no such
        /// bouquet, 409 when another bouquet holds that name in any spelling.
        /// </returns>
        /// <remarks>
        /// The identifier travels in the path and never in the body, for the reason the accounts
        /// endpoint gives: the path is the honest statement of which row is meant. A body that
        /// names nothing is a success rather than a complaint, the convention every other PATCH
        /// on this API follows.
        /// </remarks>
        [HttpPatch("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditBouquetsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateBouquet(
            [FromRoute] Guid id,
            [FromBody] UpdateBouquetRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (request.Name is not null && string.IsNullOrWhiteSpace(request.Name))
            {
                return ApiResults.ValidationFailed(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [nameof(UpdateBouquetRequest.Name)] = [NameHint]
                });
            }

            return Answer(await _bouquets.UpdateAsync(
                id,
                new BouquetPatch { Name = request.Name },
                cancellationToken));
        }

        /// <summary>
        /// Deletes a bouquet, but only while it holds nothing.
        /// </summary>
        /// <param name="id">Identifier of the bouquet to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>bouquet_deleted</c>, 404 when there is no such bouquet, 409
        /// <c>bouquet_not_empty</c> naming how many items are still inside it.
        /// </returns>
        /// <remarks>
        /// A <c>DELETE</c> that answers 200 rather than 204, because it has something to say:
        /// the difference between a bouquet that was deleted and one that was not there to
        /// delete is the whole answer, and an empty body cannot say it.
        ///
        /// It is a refusal and not a cascade: an operator is told which package is standing in
        /// the way rather than watching a screenful of memberships vanish, and the foreign key
        /// behind it is <c>ON DELETE RESTRICT</c> so the rule survives a second code path.
        /// </remarks>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditBouquetsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteBouquet(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await _bouquets.DeleteAsync(id, cancellationToken));
        }

        /// <summary>
        /// Replaces the order of the bouquets with the one the body names.
        /// </summary>
        /// <param name="request">Every bouquet, in the new order.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>bouquets_reordered</c>, 400 when the body broke a rule on itself (identifiers
        /// that are not uuids, or a list that does not name every bouquet exactly once), 400
        /// <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// A whole-list write rather than a per-row position patch, for the reason the class
        /// remarks give. A subset, a duplicate or a foreign identifier is refused whole rather
        /// than partly applied: half an order is not an order, it is two rows sharing a
        /// position.
        /// </remarks>
        [HttpPut("order")]
        [Authorize(Policy = AuthenticationExtensions.EditBouquetsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ReorderBouquets(
            [FromBody] ReorderBouquetsRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var ids = ParseIds(request.OrderedIds, nameof(ReorderBouquetsRequest.OrderedIds), BouquetIdsHint, errors);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            return Answer(await _bouquets.ReorderAsync(
                new ReorderBouquets { OrderedIds = ids! },
                cancellationToken));
        }

        /// <summary>
        /// Reads the two lists one tab of the content manager draws for one kind of content.
        /// </summary>
        /// <param name="id">Identifier of the bouquet.</param>
        /// <param name="contentType">
        /// Which kind the tab is about, as the lower-case enum member name -
        /// <c>videostream</c>, <c>audiostream</c>, <c>videoondemand</c> or <c>series</c>.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 with the two lists, 400 when the type names no kind that exists, 404 when no
        /// bouquet answers to that identifier.
        /// </returns>
        /// <remarks>
        /// Both lists in one answer, for the reason the response's remarks give: they are two
        /// halves of one sentence and a caller that fetched them apart could paint a list that
        /// had never existed. Neither is paged, because the manager moves items between two
        /// complete sets - a pager over either half would make "select all" mean "the page I am
        /// looking at".
        /// </remarks>
        [HttpGet("{id:guid}/content")]
        [Authorize(Policy = AuthenticationExtensions.ViewBouquetsPolicy)]
        [ProducesResponseType<AdminBouquetContentResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetContent(
            [FromRoute] Guid id,
            [FromQuery] string? contentType,
            CancellationToken cancellationToken)
        {
            NoStore();

            var wantedType = ParseTypeOrNull(contentType);

            if (wantedType is null)
            {
                return ApiResults.ValidationFailed(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["contentType"] = [TypeHint]
                });
            }

            var content = await _bouquets.GetContentAsync(id, wantedType.Value, cancellationToken);

            return content is null
                ? Answer(new BouquetOutcome { Action = BouquetAction.NotFound, BouquetId = id })
                : Ok(new AdminBouquetContentResponse
                {
                    Members = content.Members.Select(ToMember).ToList(),
                    Available = content.Available.Select(ToMember).ToList()
                });
        }

        /// <summary>
        /// Replaces the content of one kind that a bouquet holds, in the order the body names.
        /// </summary>
        /// <param name="id">Identifier of the bouquet.</param>
        /// <param name="request">The kind and every item of it, in the new order.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>bouquet_content_set</c>, 400 when the body broke a rule on itself (an unknown
        /// kind, identifiers that are not uuids, or an item named twice), 404 when there is no
        /// such bouquet, 400 <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// <b>The order in the body is the order the bouquet holds.</b> Positions 1..N are
        /// written in exactly that sequence, so what the operator arranged on screen is what the
        /// next read answers - that is the promise this endpoint exists to keep, and the reason
        /// it is a replacement rather than a set of additions.
        ///
        /// An empty list is a statement rather than an omission: it empties the bouquet of that
        /// kind. An absent list is refused, because "I forgot the field" and "empty it" must not
        /// be the same request.
        /// </remarks>
        [HttpPut("{id:guid}/content")]
        [Authorize(Policy = AuthenticationExtensions.EditBouquetsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetContent(
            [FromRoute] Guid id,
            [FromBody] SetBouquetContentRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            ContentType? type = null;
            if (ParseTypeOrNull(request.ContentType) is { } parsedType)
            {
                type = parsedType;
            }
            else
            {
                errors[nameof(SetBouquetContentRequest.ContentType)] = [TypeHint];
            }

            // An empty list is allowed here, unlike the reorder: "the bouquet holds none of this
            // kind" is a state the content manager produces by moving everything out of it.
            var ids = ParseIds(
                request.ContentIds,
                nameof(SetBouquetContentRequest.ContentIds),
                ContentIdsHint,
                errors,
                allowEmpty: true);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            return Answer(await _bouquets.SetContentAsync(
                id,
                new SetBouquetContent
                {
                    ContentType = type!.Value,
                    ContentIds = ids!
                },
                cancellationToken));
        }

        /// <summary>
        /// Turns the text of a query or a body field into the kind of content it names, or
        /// nothing at all.
        /// </summary>
        /// <remarks>
        /// Matched member by member rather than with an ordered value, and compared after
        /// lowering, so <c>VideoStream</c> is the same request as <c>videostream</c>. Enum
        /// binding is deliberately not used, for the reason the accounts controller gives: it
        /// takes a member name spelled in full and would refuse the conventional lower-case
        /// one.
        /// </remarks>
        private static ContentType? ParseTypeOrNull(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "videostream" => ContentType.VideoStream,
                "audiostream" => ContentType.AudioStream,
                "videoondemand" => ContentType.VideoOnDemand,
                "series" => ContentType.Series,
                _ => null
            };

        /// <summary>
        /// Turns the identifiers a body sent into the rows they name, or nothing at all once
        /// one of them is wrong.
        /// </summary>
        /// <remarks>
        /// Duplicates are <i>not</i> collapsed here, unlike the bulk endpoints' lists: order is
        /// the whole content of a list in this controller, and two entries naming one row are
        /// two claims about one position. They pass through as they arrived and the service
        /// refuses them, one refusal about one list.
        /// </remarks>
        private static IReadOnlyList<Guid>? ParseIds(
            IReadOnlyList<string>? ids,
            string field,
            string hint,
            Dictionary<string, string[]> errors,
            bool allowEmpty = false)
        {
            if (ids is null || (ids.Count is 0 && !allowEmpty))
            {
                errors[field] = [hint];
                return null;
            }

            var parsed = new List<Guid>(ids.Count);

            foreach (var text in ids)
            {
                if (!Guid.TryParse(text, out var id))
                {
                    errors[field] = [hint];
                    return null;
                }

                parsed.Add(id);
            }

            return parsed;
        }

        /// <summary>
        /// Turns the service's answer into the status and body a client is told, the one place
        /// where a <see cref="BouquetAction"/> becomes a number.
        /// </summary>
        private static IActionResult Answer(BouquetOutcome outcome)
        {
            var (status, body) = BouquetResponses.Describe(outcome);

            return new ObjectResult(body) { StatusCode = status };
        }

        private static AdminBouquetItemResponse ToItem(BouquetListItem item) => new()
        {
            Id = item.Id.ToString(),
            Name = item.Name,
            SortOrder = item.SortOrder,
            VideoStreamCount = item.VideoStreamCount,
            AudioStreamCount = item.AudioStreamCount,
            VideoOnDemandCount = item.VideoOnDemandCount,
            SeriesCount = item.SeriesCount
        };

        private static BouquetMemberResponse ToMember(BouquetMember member) => new()
        {
            Id = member.Id.ToString(),
            Name = member.Name,
            CategoryId = member.CategoryId.ToString(),
            CategoryName = member.CategoryName
        };
    }
}
