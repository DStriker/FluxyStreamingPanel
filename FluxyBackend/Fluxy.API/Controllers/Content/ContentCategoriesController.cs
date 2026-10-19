using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Content;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Content;
using Fluxy.Core.Models.Streams;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Content
{
    /// <summary>
    /// Managing the categories content is filed under: every category of one kind of
    /// content, and the writes that create, rename, delete and re-arrange them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One policy on the class, not one per action: every route here is <c>AdminOnly</c>, so
    /// an action added later is an admin action unless its author changes the class. No
    /// permission bit sits on top of it, on purpose - the sections this controller serves are
    /// admin-only in the interface as well, and the permission catalog grows only when a
    /// delegate-able job actually appears, not in advance of one.
    /// </para>
    /// <para>
    /// <b>No captcha and no attempt window</b>, for the reason the accounts controller gives:
    /// both exist to guard a request with no session at all, and everything here is behind an
    /// admin session and a fresh antiforgery pair. This is a recorded departure from the
    /// registration surface's shape, agreed for the whole admin area.
    /// </para>
    /// <para>
    /// <b>The list is not paged, deliberately.</b> A type's categories are a screenful by
    /// construction - they are the section headings of a content grid, not a per-row record
    /// - so the read answers all of them at once and the response carries no <c>total</c>,
    /// no <c>page</c> and no <c>pageSize</c>. A pager over this list would promise a second
    /// page that has no reason to exist; if a type ever outgrows one screen, that is the
    /// moment to add paging, not before.
    /// </para>
    /// <para>
    /// <b>The reorder is one whole-list write.</b> <c>PUT /admin/content-categories/order</c>
    /// replaces the order sub-resource of the collection: the body names the type and every
    /// one of its categories in the new order, and the service writes positions 1..N over
    /// them in one save. A per-row <c>PATCH</c> of a moved position plus shifting the
    /// neighbours would be several writes where one atomic one exists, and the caller - a
    /// screen that has just shown a drag over the entire list - already knows all of it.
    /// </para>
    /// <para>
    /// Both reads carry <c>Cache-Control: no-store</c> through
    /// <see cref="ApiControllerBase.NoStore"/>: this is the section structure of a private
    /// area, and a shared cache that kept one would hand it to the next visitor.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/content-categories")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class ContentCategoriesController : ApiControllerBase
    {
        /// <summary>Sentence a type that names no known member gets.</summary>
        private const string TypeHint =
            "The content type must be one of: videostream, audiostream, videoondemand, series.";

        /// <summary>Sentence a name that is blank or too long gets.</summary>
        private const string NameHint =
            "The name must be between 1 and 100 characters.";

        /// <summary>Sentence a set of identifiers that is not all uuids gets.</summary>
        private const string IdsHint = "Every identifier must be the uuid of a category.";

        private readonly IContentCategoryService _categories;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentCategoriesController"/> class.
        /// </summary>
        /// <param name="categories">Service that reads and changes the categories.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public ContentCategoriesController(
            IContentCategoryService categories,
            IAntiforgery antiforgery,
            ILogger<ContentCategoriesController> logger)
            : base(antiforgery, logger)
        {
            _categories = categories;
        }

        /// <summary>
        /// Reports every category of one kind of content, in the order an operator arranged
        /// them in.
        /// </summary>
        /// <param name="type">
        /// The kind of content whose categories are wanted, as the name of the enum member
        /// compared case insensitively - <c>videostream</c>, <c>audiostream</c>,
        /// <c>videoondemand</c> or <c>series</c>.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with the whole list, or 400 when the type names no kind that exists.</returns>
        /// <remarks>
        /// The type is required rather than defaulted, and a value naming no member is
        /// refused rather than ignored: "no filter" would answer with four unrelated lists
        /// folded into one, which is a question nobody asked and no screen draws.
        ///
        /// No antiforgery pair: the method changes nothing.
        /// </remarks>
        [HttpGet]
        [ProducesResponseType<AdminContentCategoryListResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetCategories(
            [FromQuery] string? type,
            CancellationToken cancellationToken)
        {
            NoStore();

            var wantedType = ParseTypeOrNull(type);

            if (wantedType is null)
            {
                return ApiResults.ValidationFailed(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["type"] = [TypeHint]
                });
            }

            var items = await _categories.GetAllAsync(wantedType.Value, cancellationToken);

            return Ok(new AdminContentCategoryListResponse
            {
                Items = items.Select(ToItem).ToList()
            });
        }

        /// <summary>
        /// Creates a category at the end of its type's list.
        /// </summary>
        /// <param name="request">The category to create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 201 with a <c>Location</c> naming the new category, 400 with the rejected fields,
        /// 409 when another category of the same type holds that name, 400
        /// <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// The 201 carries a <c>Location</c>, and it is honest: the row exists in full the
        /// moment it is written, this very controller serves the list it appears in, and
        /// nothing stands between creation and addressability.
        ///
        /// There is deliberately no <c>sortOrder</c> in the body. A new category joins the
        /// end, and where the end is (the highest position plus one) is the service's fact to
        /// read from the database - a caller naming a position would be one stale screen away
        /// from overwriting a row an operator has just moved.
        /// </remarks>
        [HttpPost]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateCategory(
            [FromBody] CreateContentCategoryRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                errors[nameof(CreateContentCategoryRequest.Name)] = [NameHint];
            }

            ContentType? type = null;
            if (ParseTypeOrNull(request.Type) is { } parsedType)
            {
                type = parsedType;
            }
            else
            {
                errors[nameof(CreateContentCategoryRequest.Type)] = [TypeHint];
            }

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _categories.CreateAsync(
                new NewContentCategory
                {
                    Name = request.Name?.Trim() ?? string.Empty,
                    Type = type!.Value
                },
                cancellationToken);

            var (statusCode, body) = ContentCategoryResponses.Describe(outcome);

            if (statusCode == StatusCodes.Status201Created)
            {
                Response.Headers.Location = $"/admin/content-categories/{outcome.CategoryId}";
            }

            return StatusCode(statusCode, body);
        }

        /// <summary>
        /// Renames one category.
        /// </summary>
        /// <param name="id">Identifier of the category to change, in the path.</param>
        /// <param name="request">The fields to change. An absent field is left alone.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 when it was written, 400 with the rejected fields, 404 when there is no such
        /// category, 409 when another category of the same type holds that name.
        /// </returns>
        /// <remarks>
        /// The identifier travels in the path and never in the body, for the reason the
        /// accounts endpoint gives: the path is the honest statement of which row is meant.
        ///
        /// A body that names nothing is a success rather than a complaint, the convention
        /// every other PATCH on this API follows. There is nothing else to change: the type
        /// never moves, and the position is not a field one row writes - the list is
        /// re-arranged wholesale through <c>PUT order</c>.
        /// </remarks>
        [HttpPatch("{id:guid}")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateCategory(
            [FromRoute] Guid id,
            [FromBody] UpdateContentCategoryRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (request.Name is not null && string.IsNullOrWhiteSpace(request.Name))
            {
                errors[nameof(UpdateContentCategoryRequest.Name)] = [NameHint];
            }

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _categories.UpdateAsync(
                id,
                new ContentCategoryPatch { Name = request.Name },
                cancellationToken);

            return Answer(outcome);
        }

        /// <summary>
        /// Deletes a category.
        /// </summary>
        /// <param name="id">Identifier of the category to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>content_category_deleted</c>, 404 when there is no such category, 409
        /// <c>content_category_in_use</c> naming how many streams are still filed under it.
        /// </returns>
        /// <remarks>
        /// A <c>DELETE</c> that answers 200 rather than 204, because it has something to say:
        /// the difference between a category that was deleted and one that was not there to
        /// delete is the whole answer, and an empty body cannot say it.
        ///
        /// The "in use" refusal counts streams rather than every kind of content, because a
        /// stream is what this installation can file under a category today; the check grows
        /// a second count when a second kind can. It is a refusal and not a cascade for the
        /// reason the accounts' group is one: an operator is told which category is standing
        /// in the way, instead of a category vanishing along with a screenful of rows nobody
        /// asked about.
        /// </remarks>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteCategory(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await _categories.DeleteAsync(id, cancellationToken));
        }

        /// <summary>
        /// Replaces the order of one type's categories with the one the body names.
        /// </summary>
        /// <param name="request">The type and every one of its categories, in the new order.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>content_categories_reordered</c>, 400 when the body broke a rule on itself
        /// (an unknown type, identifiers that are not uuids, or a list that does not name
        /// every category of the type exactly once), 400 <c>csrf_invalid</c> without the
        /// antiforgery pair.
        /// </returns>
        /// <remarks>
        /// A whole-list write rather than a per-row position patch, for the reason the class
        /// remarks give: one save of positions 1..N over the named rows is atomic, and the
        /// caller - a screen that has just shown a drag over the entire list - already knows
        /// all of it. A subset, a duplicate or a foreign identifier is refused whole rather
        /// than partly applied: half an order is not an order, it is two rows sharing a
        /// position.
        /// </remarks>
        [HttpPut("order")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ReorderCategories(
            [FromBody] ReorderContentCategoriesRequest request,
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
                errors[nameof(ReorderContentCategoriesRequest.ContentType)] = [TypeHint];
            }

            var ids = ParseIds(request.OrderedIds, errors);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _categories.ReorderAsync(
                new ReorderContentCategories
                {
                    Type = type!.Value,
                    OrderedIds = ids!
                },
                cancellationToken);

            return Answer(outcome);
        }

        /// <summary>
        /// Turns the text of a query or a body field into the kind of content it names, or
        /// nothing at all.
        /// </summary>
        /// <remarks>
        /// Matched member by member rather than with an ordered value, because the same
        /// sentence the accounts list gives applies: a kind is a member of an enum, and a
        /// number outside them would be a kind every reader has to guess about. Compared
        /// after lowering, so <c>VideoStream</c> is the same request as <c>videostream</c>.
        /// Enum binding is deliberately not used, for the reason the accounts controller
        /// gives: it takes a member name spelled in full and would refuse the conventional
        /// lower-case one.
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
        /// Turns the identifiers a reorder body sent into the rows they name, or nothing at
        /// all once one of them is wrong.
        /// </summary>
        /// <remarks>
        /// Duplicates are <i>not</i> collapsed here, unlike the bulk endpoints' lists: order
        /// is the whole content of this request, and two entries naming one row are two
        /// claims about one position. They pass through as they arrived and the service
        /// refuses them together with every other list that does not name each row exactly
        /// once - one refusal about one list, rather than a parse error pretending to be a
        /// different mistake.
        /// </remarks>
        private static IReadOnlyList<Guid>? ParseIds(
            IReadOnlyList<string>? ids,
            Dictionary<string, string[]> errors)
        {
            if (ids is null || ids.Count is 0)
            {
                errors[nameof(ReorderContentCategoriesRequest.OrderedIds)] = [IdsHint];
                return null;
            }

            var parsed = new List<Guid>(ids.Count);

            foreach (var text in ids)
            {
                if (!Guid.TryParse(text, out var id))
                {
                    errors[nameof(ReorderContentCategoriesRequest.OrderedIds)] = [IdsHint];
                    return null;
                }

                parsed.Add(id);
            }

            return parsed;
        }

        /// <summary>
        /// Turns the service's answer into the status and body a client is told, the one
        /// place where a <see cref="ContentCategoryAction"/> becomes a number.
        /// </summary>
        private static IActionResult Answer(ContentCategoryOutcome outcome)
        {
            var (status, body) = ContentCategoryResponses.Describe(outcome);

            return new ObjectResult(body) { StatusCode = status };
        }

        private static AdminContentCategoryItemResponse ToItem(ContentCategory item) => new()
        {
            Id = item.Id.ToString(),
            Name = item.Name,
            ContentType = item.Type.ToString(),
            SortOrder = item.SortOrder
        };
    }
}
