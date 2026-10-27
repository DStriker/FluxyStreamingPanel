using System.Globalization;
using System.Net;
using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Vod;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Interfaces;
using Fluxy.Core.Vod.Commands;
using Fluxy.Core.Vod.Enums;
using Fluxy.Core.Vod.Interfaces;
using Fluxy.Core.Vod.Models;
using Fluxy.Core.Vod.Outcomes;
using Fluxy.Core.Vod.ReadModels;
using Fluxy.Core.Vod.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace Fluxy.API.Controllers.Content
{
    /// <summary>
    /// Managing the on-demand catalogue: one page of the table, one video in full, the writes
    /// that create, change and delete them, and the endpoint that delivers them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two policies on every action: the class carries <c>AdminOnly</c> the way the streams
    /// controller does, and each action adds its own permission on top. Read and write are
    /// separate grants, so a group which may watch the catalogue and a group which may run it
    /// stay two different things.
    /// </para>
    /// <para>
    /// <b>No captcha and no attempt window</b>, for the reason the rest of the admin area
    /// gives: both guard a request with no session at all, and everything here is behind an
    /// admin session and a fresh antiforgery pair.
    /// </para>
    /// <para>
    /// <b>The delivery rule lives in <see cref="PlayVideo"/>.</b> A redirect item answers with
    /// a 302 to its source; a relay item is served by this installation - a local file with
    /// range support, or a remote address streamed through this process. That is the one place
    /// the two kinds differ, and it is why the list response does not carry the source at all.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/video-on-demand")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class VideoOnDemandController : ApiControllerBase
    {
        /// <summary>Sentence a kind that names no member gets.</summary>
        private const string TypeHint = "The type must be either relay or redirect.";

        /// <summary>Sentence a state that names no member gets.</summary>
        private const string StatusHint = "The status must be either active or inactive.";

        /// <summary>Sentence a column that names nothing gets.</summary>
        private const string SortByHint =
            "The sort field must be one of: name, category, status, type, releasedate, rating.";

        /// <summary>Sentence a direction that names nothing gets.</summary>
        private const string SortOrderHint = "The sort order must be either asc or desc.";

        /// <summary>Sentence an identifier that is not a uuid gets.</summary>
        private const string IdHint = "That value must be the uuid of the row it names.";

        /// <summary>Media types a browser may be handed for a local file.</summary>
        private static readonly FileExtensionContentTypeProvider ContentTypes = new();

        private readonly IVodService _items;
        private readonly IBouquetService _bouquets;
        private readonly IHttpClientFactory _http;
        private readonly IWebHostEnvironment _environment;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoOnDemandController"/> class.
        /// </summary>
        /// <param name="items">Service that reads and changes the catalogue.</param>
        /// <param name="bouquets">
        /// Service that answers which bouquets a video belongs to, for the form's picker.
        /// </param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        /// <param name="http">Factory for the client that relays a remote source.</param>
        /// <param name="environment">Environment whose content root resolves a relative path.</param>
        public VideoOnDemandController(
            IVodService items,
            IBouquetService bouquets,
            IAntiforgery antiforgery,
            ILogger<VideoOnDemandController> logger,
            IHttpClientFactory http,
            IWebHostEnvironment environment)
            : base(antiforgery, logger)
        {
            _items = items;
            _bouquets = bouquets;
            _http = http;
            _environment = environment;
        }

        /// <summary>
        /// Reports one page of the table, filtered and ordered the way the query asked.
        /// </summary>
        /// <remarks>
        /// Search, filters, order and paging live here rather than in the browser because the
        /// pager shows a <c>total</c>. The bounds are refused rather than clamped, and a value
        /// that names nothing is refused rather than treated as "no filter".
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewVideoOnDemandPolicy)]
        [ProducesResponseType<AdminVodPageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetVideos(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = VodListLimits.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] string? categoryId = null,
            [FromQuery] string? status = null,
            [FromQuery] string? type = null,
            [FromQuery] string? sortBy = "name",
            [FromQuery] string? sortOrder = "asc",
            CancellationToken cancellationToken = default)
        {
            NoStore();

            var field = ParseSortField(sortBy);
            var order = ParseSortOrder(sortOrder);
            var wantedStatus = VodTextParser.ParseStatusOrNull(status);
            var wantedType = VodTextParser.ParseTypeOrNull(type);

            Guid? wantedCategory = null;
            var categoryOk = true;

            if (!string.IsNullOrWhiteSpace(categoryId))
            {
                categoryOk = Guid.TryParse(categoryId, out var parsedCategory);
                wantedCategory = categoryOk ? parsedCategory : null;
            }

            var statusOk = wantedStatus is not null || string.IsNullOrWhiteSpace(status);
            var typeOk = wantedType is not null || string.IsNullOrWhiteSpace(type);

            if (page < 1
                || pageSize is < 1 or > VodListLimits.MaxPageSize
                || (search?.Length ?? 0) > VodListLimits.MaxSearchLength
                || field is null
                || order is null
                || !categoryOk
                || !statusOk
                || !typeOk)
            {
                return ApiResults.ValidationFailed(ListErrors(
                    page,
                    pageSize,
                    search,
                    field,
                    order,
                    categoryOk,
                    statusOk,
                    typeOk));
            }

            var list = await _items.GetPageAsync(
                page,
                pageSize,
                search,
                wantedCategory,
                wantedStatus,
                wantedType,
                field.Value,
                order.Value,
                cancellationToken);

            return Ok(new AdminVodPageResponse
            {
                Items = list.Items.Select(ToItem).ToList(),
                Total = list.Total,
                Page = list.Page,
                PageSize = list.PageSize
            });
        }

        /// <summary>Reports one video in full, for the edit form.</summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.ViewVideoOnDemandPolicy)]
        [ProducesResponseType<AdminVodDetailResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetVideo(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            NoStore();

            var item = await _items.GetAsync(id, cancellationToken);

            return item is null
                ? Answer(new VodOutcome { Action = VodAction.NotFound, VodId = id })
                : Ok(ToDetail(
                    item,
                    await _bouquets.GetItemBouquetIdsAsync(ContentType.VideoOnDemand, id, cancellationToken)));
        }

        /// <summary>Creates a video in its category.</summary>
        /// <remarks>
        /// The 201 carries a <c>Location</c>, and it is honest: the row exists in full the
        /// moment it is written and this very controller serves the address it names.
        /// </remarks>
        [HttpPost]
        [Authorize(Policy = AuthenticationExtensions.EditVideoOnDemandPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateVideo(
            [FromBody] CreateVodItemRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var type = VodTextParser.ParseTypeOrNull(request.Type);
            if (type is null)
            {
                errors[nameof(CreateVodItemRequest.Type)] = [TypeHint];
            }

            // An absent status keeps the safe default: a new video is not published until its
            // operator says so. A value that names no member is refused rather than silently
            // replaced by that default.
            var status = VodTextParser.ParseStatusOrNull(request.Status);
            if (status is null)
            {
                if (string.IsNullOrWhiteSpace(request.Status))
                {
                    status = VodStatus.Inactive;
                }
                else
                {
                    errors[nameof(CreateVodItemRequest.Status)] = [StatusHint];
                }
            }

            Guid? categoryId = null;
            if (string.IsNullOrWhiteSpace(request.CategoryId)
                || !Guid.TryParse(request.CategoryId, out var parsedCategory))
            {
                errors[nameof(CreateVodItemRequest.CategoryId)] = [IdHint];
            }
            else
            {
                categoryId = parsedCategory;
            }

            // A set rather than an order: the service appends the video to the end of every
            // bouquet it joins, and the position inside one belongs to the bouquet's own screen.
            var bouquetIds = BouquetTextParser.ParseOrNull(
                request.BouquetIds, nameof(CreateVodItemRequest.BouquetIds), errors);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _items.CreateAsync(
                new NewVodItem
                {
                    Name = request.Name?.Trim() ?? string.Empty,
                    Status = status!.Value,
                    Type = type!.Value,
                    CategoryId = categoryId!.Value,
                    Source = request.Source?.Trim() ?? string.Empty,
                    SubtitleUrl = request.SubtitleUrl,
                    ImdbId = request.ImdbId,
                    PosterUrl = request.PosterUrl,
                    BackdropUrl = request.BackdropUrl,
                    Plot = request.Plot,
                    Cast = request.Cast ?? [],
                    Director = request.Director,
                    Genres = request.Genres ?? [],
                    ReleaseDate = request.ReleaseDate,
                    Rating = request.Rating,
                    CountryCode = request.CountryCode,
                    BouquetIds = bouquetIds
                },
                cancellationToken);

            var (statusCode, body) = VodResponses.Describe(outcome);

            if (statusCode == StatusCodes.Status201Created)
            {
                Response.Headers.Location = $"/admin/video-on-demand/{outcome.VodId}";
            }

            return StatusCode(statusCode, body);
        }

        /// <summary>Changes a video. Every field the body can name is optional.</summary>
        /// <remarks>
        /// A body that names nothing is a success rather than a complaint, the convention every
        /// other PATCH on this API follows. <c>releaseDate</c> and <c>rating</c> travel as text
        /// so that an absent value keeps and an empty one clears.
        /// </remarks>
        [HttpPatch("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditVideoOnDemandPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateVideo(
            [FromRoute] Guid id,
            [FromBody] UpdateVodItemRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            VodType? type = null;
            if (!string.IsNullOrWhiteSpace(request.Type))
            {
                type = VodTextParser.ParseTypeOrNull(request.Type);
                if (type is null)
                {
                    errors[nameof(UpdateVodItemRequest.Type)] = [TypeHint];
                }
            }

            VodStatus? status = null;
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                status = VodTextParser.ParseStatusOrNull(request.Status);
                if (status is null)
                {
                    errors[nameof(UpdateVodItemRequest.Status)] = [StatusHint];
                }
            }

            Guid? categoryId = null;
            if (!string.IsNullOrWhiteSpace(request.CategoryId))
            {
                if (Guid.TryParse(request.CategoryId, out var parsedCategory))
                {
                    categoryId = parsedCategory;
                }
                else
                {
                    errors[nameof(UpdateVodItemRequest.CategoryId)] = [IdHint];
                }
            }

            // Parsed before the check, so a broken identifier is refused with the field named
            // rather than silently dropped. Null keeps every link; an empty list clears them.
            var bouquetIds = BouquetTextParser.ParseOrNull(
                request.BouquetIds, nameof(UpdateVodItemRequest.BouquetIds), errors);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _items.UpdateAsync(
                id,
                new VodItemPatch
                {
                    Name = request.Name,
                    Status = status,
                    Type = type,
                    CategoryId = categoryId,
                    BouquetIds = bouquetIds,
                    Source = request.Source,
                    SubtitleUrl = request.SubtitleUrl,
                    ImdbId = request.ImdbId,
                    PosterUrl = request.PosterUrl,
                    BackdropUrl = request.BackdropUrl,
                    Plot = request.Plot,
                    Cast = request.Cast,
                    Director = request.Director,
                    Genres = request.Genres,
                    ReleaseDate = request.ReleaseDate,
                    Rating = request.Rating,
                    CountryCode = request.CountryCode
                },
                cancellationToken);

            return Answer(outcome);
        }

        /// <summary>Deletes a video.</summary>
        /// <remarks>
        /// A <c>DELETE</c> that answers 200 rather than 204, because it has something to say:
        /// the difference between a video that was deleted and one that was not there to
        /// delete is the whole answer.
        /// </remarks>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditVideoOnDemandPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteVideo(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await _items.DeleteAsync(id, cancellationToken));
        }

        /// <summary>
        /// Delivers the video: a redirect for a <c>Redirect</c> item, or the bytes themselves
        /// for a <c>Relay</c> one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A <c>Relay</c> item names either an absolute http(s) address or a file on this
        /// server. A file is served through <see cref="PhysicalFileResult"/> with range
        /// processing, which is what lets a browser seek. An address is fetched with
        /// <see cref="HttpCompletionOption.ResponseHeadersRead"/> and copied to the response
        /// as it arrives, forwarding the <c>Range</c> header and the length and range headers
        /// of the answer, so seeking works through the proxy too.
        /// </para>
        /// <para>
        /// Neither path writes anything, so there is no antiforgery pair. A <c>Redirect</c>
        /// item whose source is not an absolute http(s) address is refused rather than
        /// redirected to a path no browser could fetch; an inactive item is still playable,
        /// because an operator previewing a file before publishing it is exactly who this
        /// endpoint serves.
        /// </para>
        /// </remarks>
        [HttpGet("{id:guid}/play")]
        [Authorize(Policy = AuthenticationExtensions.ViewVideoOnDemandPolicy)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status302Found)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> PlayVideo(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            var item = await _items.GetAsync(id, cancellationToken);

            if (item is null)
            {
                return Answer(new VodOutcome { Action = VodAction.NotFound, VodId = id });
            }

            if (item.Type is VodType.Redirect)
            {
                return IsAbsoluteHttp(item.Source)
                    ? Redirect(item.Source)
                    : NotPlayable();
            }

            if (IsAbsoluteHttp(item.Source))
            {
                return await RelayAsync(item.Source, cancellationToken);
            }

            if (ResolveLocalFile(item.Source) is { } path)
            {
                return PhysicalFile(
                    path,
                    ContentTypes.TryGetContentType(path, out var contentType)
                        ? contentType
                        : "application/octet-stream",
                    enableRangeProcessing: true);
            }

            return NotPlayable();
        }

        /// <summary>Copies a remote source to the response, honouring a range request.</summary>
        private async Task<IActionResult> RelayAsync(string address, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, address);

            if (!string.IsNullOrWhiteSpace(Request.Headers.Range.ToString()))
            {
                request.Headers.TryAddWithoutValidation(
                    "Range",
                    Request.Headers.Range.ToString());
            }

            HttpResponseMessage response;

            try
            {
                response = await _http.CreateClient().SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
            }
            catch (HttpRequestException)
            {
                return StatusCode(StatusCodes.Status502BadGateway, VodResponses.Unavailable());
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return StatusCode(StatusCodes.Status502BadGateway, VodResponses.Unavailable());
            }

            using (response)
            {
                if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.PartialContent))
                {
                    return StatusCode(
                        StatusCodes.Status502BadGateway,
                        VodResponses.Unavailable());
                }

                Response.StatusCode = (int)response.StatusCode;

                if (response.Content.Headers.ContentType is { } contentType)
                {
                    Response.ContentType = contentType.ToString();
                }

                Response.ContentLength = response.Content.Headers.ContentLength;

                if (response.Content.Headers.ContentRange is { } range)
                {
                    Response.Headers["Content-Range"] = range.ToString();
                }

                var acceptRanges = response.Headers.AcceptRanges.ToString();
                if (!string.IsNullOrWhiteSpace(acceptRanges))
                {
                    Response.Headers["Accept-Ranges"] = acceptRanges;
                }

                await response.Content.CopyToAsync(Response.Body, cancellationToken);

                return new EmptyResult();
            }
        }

        /// <summary>Whether an address is an absolute http or https one.</summary>
        private static bool IsAbsoluteHttp(string value)
            => Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        /// <summary>
        /// Resolves a stored path to an existing file, rooting a relative one at the content
        /// root so a bare file name works however the host was started.
        /// </summary>
        private string? ResolveLocalFile(string source)
        {
            var path = Path.IsPathRooted(source)
                ? source.Trim()
                : Path.Combine(_environment.ContentRootPath, source.Trim());

            return System.IO.File.Exists(path) ? path : null;
        }

        /// <summary>The 409 a source that names neither a URL nor a readable file gets.</summary>
        private IActionResult NotPlayable()
            => StatusCode(StatusCodes.Status409Conflict, VodResponses.NotPlayable());

        /// <summary>Turns <c>asc</c> / <c>desc</c> into an order, or nothing at all.</summary>
        private static VodSortOrder? ParseSortOrder(string? value)
            => SortOrderParser.Parse(
                value,
                VodSortOrder.Ascending,
                VodSortOrder.Descending);

        /// <summary>Turns the name of a column into the field to order by, or nothing at all.</summary>
        private static VodSortField? ParseSortField(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "name" => VodSortField.Name,
                "category" => VodSortField.Category,
                "status" => VodSortField.Status,
                "type" => VodSortField.Type,
                "releasedate" => VodSortField.ReleaseDate,
                "rating" => VodSortField.Rating,
                _ => null
            };

        /// <summary>
        /// Which of the bounds a request broke, keyed by the camelCase name of the query
        /// parameter so the reason lands under the input a client would fix.
        /// </summary>
        private static Dictionary<string, string[]> ListErrors(
            int page,
            int pageSize,
            string? search,
            VodSortField? field,
            VodSortOrder? order,
            bool categoryOk,
            bool statusOk,
            bool typeOk)
        {
            var errors = ListQueryErrors.Common(
                page,
                pageSize,
                search,
                VodListLimits.MaxPageSize,
                VodListLimits.MaxSearchLength);

            if (field is null)
            {
                errors["sortBy"] = [SortByHint];
            }

            if (order is null)
            {
                errors["sortOrder"] = [SortOrderHint];
            }

            if (!categoryOk)
            {
                errors["categoryId"] = [IdHint];
            }

            if (!statusOk)
            {
                errors["status"] = [StatusHint];
            }

            if (!typeOk)
            {
                errors["type"] = [TypeHint];
            }

            return errors;
        }

        /// <summary>
        /// Turns the service's answer into the status and body a client is told, the one place
        /// where a <see cref="VodAction"/> becomes a number.
        /// </summary>
        private static IActionResult Answer(VodOutcome outcome)
        {
            var (status, body) = VodResponses.Describe(outcome);

            return new ObjectResult(body) { StatusCode = status };
        }

        private static AdminVodItemResponse ToItem(VodListItem item) => new()
        {
            Id = item.Id.ToString(),
            Name = item.Name,
            Status = item.Status.ToString(),
            Type = item.Type.ToString(),
            CategoryId = item.CategoryId.ToString(),
            CategoryName = item.CategoryName,
            PosterUrl = item.PosterUrl,
            ReleaseDate = FormatDate(item.ReleaseDate),
            Rating = item.Rating
        };

        private static AdminVodDetailResponse ToDetail(
            VodItem item,
            IReadOnlyList<Guid> bouquetIds) => new()
        {
            Id = item.Id.ToString(),
            Name = item.Name,
            Status = item.Status.ToString(),
            Type = item.Type.ToString(),
            CategoryId = item.CategoryId.ToString(),
            CategoryName = item.CategoryName,
            Source = item.Source,
            SubtitleUrl = item.SubtitleUrl,
            ImdbId = item.ImdbId,
            PosterUrl = item.PosterUrl,
            BackdropUrl = item.BackdropUrl,
            Plot = item.Plot,
            Cast = item.Cast,
            Director = item.Director,
            Genres = item.Genres,
            ReleaseDate = FormatDate(item.ReleaseDate),
            Rating = item.Rating,
            CountryCode = item.CountryCode,
            BouquetIds = bouquetIds.Select(bouquetId => bouquetId.ToString()).ToList()
        };

        private static string? FormatDate(DateOnly? value)
            => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
