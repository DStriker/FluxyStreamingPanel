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
    /// Creates on-demand videos from an uploaded M3U playlist.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A controller of its own rather than an action on <see cref="VideoOnDemandController"/>:
    /// the request is a file rather than a JSON row, the answer counts a batch rather than
    /// describing one video, and the form has neither a name nor a source - both come from the
    /// playlist. The seam is the shape of the request and the answer, the same seam that gave
    /// the bulk endpoint its own controller.
    /// </para>
    /// <para>
    /// The antiforgery check runs before the form is read, because a request without the pair
    /// is not this application's request at all. The transport parses the form fields it can
    /// parse and refuses a value that names no member; whether the category exists and whether
    /// each line can become a video are rules of the service.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/video-on-demand/import")]
    [Authorize(Policy = AuthenticationExtensions.EditVideoOnDemandPolicy)]
    [Produces("application/json")]
    public sealed class VideoOnDemandImportController : ApiControllerBase
    {
        /// <summary>Form field that carries the uploaded file.</summary>
        private const string PlaylistField = "playlist";

        /// <summary>Sentence a type that names no member gets.</summary>
        private const string TypeHint = "The type must be either relay or redirect.";

        /// <summary>Sentence a state that names no member gets.</summary>
        private const string StatusHint = "The status must be either active or inactive.";

        /// <summary>Sentence a category that is not an identifier gets.</summary>
        private const string CategoryHint =
            "The category must be the identifier of a category, as a uuid.";

        /// <summary>Sentence a missing or unreadable file gets.</summary>
        private const string PlaylistHint = "Choose an M3U playlist file to import.";

        private readonly IVodImportService _imports;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoOnDemandImportController"/> class.
        /// </summary>
        /// <param name="imports">Service that reads the playlist and writes its entries.</param>
        /// <param name="antiforgery">Antiforgery service the base class checks with.</param>
        /// <param name="logger">Logger the base class reports through.</param>
        public VideoOnDemandImportController(
            IVodImportService imports,
            IAntiforgery antiforgery,
            ILogger<VideoOnDemandImportController> logger)
            : base(antiforgery, logger)
        {
            _imports = imports;
        }

        /// <summary>
        /// Reads a playlist and creates every video it can use.
        /// </summary>
        /// <remarks>
        /// A file that is readable answers <c>200</c> even when some entries were skipped or
        /// refused; the counts and the per-line problems state what happened. Only a request
        /// that made no sense - a missing file, a size the server will not read, an HLS
        /// playlist, a file with no video entry - is a refusal, and the code names which.
        /// </remarks>
        [HttpPost]
        [ProducesResponseType<VodImportResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status415UnsupportedMediaType)]
        public async Task<IActionResult> ImportVideos(CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            // The body is not read until the token has been checked, and the content type is
            // checked before the form reader can throw its own exception for it.
            if (!Request.HasFormContentType)
            {
                return StatusCode(
                    StatusCodes.Status415UnsupportedMediaType,
                    new MessageResponse
                    {
                        Code = "unsupported_media_type",
                        Message = "The request must be multipart/form-data."
                    });
            }

            if (Request.ContentLength is > VodImportLimits.MaxFileBytes + 1024 * 1024)
            {
                return TooLarge();
            }

            IFormCollection form;

            try
            {
                form = await Request.ReadFormAsync(cancellationToken);
            }
            catch (InvalidDataException)
            {
                return ApiResults.ValidationFailed(new Dictionary<string, string[]>(
                    StringComparer.Ordinal)
                {
                    [PlaylistField] = [PlaylistHint]
                });
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var type = VodTextParser.ParseTypeOrNull(form["type"].ToString());
            if (type is null)
            {
                errors["type"] = [TypeHint];
            }

            // An absent status keeps the safe default: a batch of thousands opens disabled.
            var statusText = form["status"].ToString();
            var status = string.IsNullOrWhiteSpace(statusText)
                ? VodStatus.Inactive
                : VodTextParser.ParseStatusOrNull(statusText);

            if (status is null)
            {
                errors["status"] = [StatusHint];
            }

            var categoryId = Guid.Empty;
            var categoryText = form["categoryId"].ToString();
            if (string.IsNullOrWhiteSpace(categoryText))
            {
                errors["categoryId"] = [CategoryHint];
            }
            else if (!Guid.TryParse(categoryText.Trim(), out categoryId) || categoryId == Guid.Empty)
            {
                errors["categoryId"] = [CategoryHint];
            }

            var file = form.Files.GetFile(PlaylistField);
            if (file is null)
            {
                errors[PlaylistField] = [PlaylistHint];
            }

            // Parsed before the error check below, so a broken identifier is refused with the
            // field named rather than silently dropped.
            var bouquetIds = BouquetTextParser.ParseFromForm(form, errors);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            if (file!.Length == 0)
            {
                return Failure(
                    StatusCodes.Status400BadRequest,
                    VodImportResponses.EmptyCode,
                    "The playlist file is empty.");
            }

            if (file.Length > VodImportLimits.MaxFileBytes)
            {
                return TooLarge();
            }

            string playlist;

            try
            {
                using var reader = new StreamReader(file.OpenReadStream());
                playlist = await reader.ReadToEndAsync(cancellationToken);
            }
            catch (IOException)
            {
                return Failure(
                    StatusCodes.Status400BadRequest,
                    VodImportResponses.UnreadableCode,
                    "The playlist file could not be read.");
            }

            var outcome = await _imports.ImportAsync(
                new NewVodImport
                {
                    Type = type!.Value,
                    Status = status!.Value,
                    CategoryId = categoryId,
                    Playlist = playlist,
                    BouquetIds = bouquetIds
                },
                cancellationToken);

            var (statusCode, body) = VodImportResponses.Describe(outcome);

            return new ObjectResult(body) { StatusCode = statusCode };
        }

        /// <summary>The answer for a file the server will not read.</summary>
        private ObjectResult TooLarge() => Failure(
            StatusCodes.Status413PayloadTooLarge,
            VodImportResponses.TooLargeCode,
            "The playlist file is larger than "
                + (VodImportLimits.MaxFileBytes / (1024 * 1024))
                + " MB.");

        /// <summary>Builds one refusal with the protocol's usual body.</summary>
        private static ObjectResult Failure(int status, string code, string message)
            => new(VodImportResponses.Failure(code, message)) { StatusCode = status };
    }
}
