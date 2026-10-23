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
    /// Creates audio streams from an uploaded M3U playlist.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A controller of its own rather than an action on <see cref="AudioStreamsController"/>:
    /// the request is a file rather than a JSON row, the answer counts a batch rather than
    /// describing one stream, and the form has neither a name nor a sources list - both come
    /// from the playlist. The seam is the shape of the request and the answer, the same seam
    /// that gave the bulk endpoint its own controller.
    /// </para>
    /// <para>
    /// The antiforgery check runs before the form is read, because a request without the pair
    /// is not this application's request at all. The transport parses the form fields it can
    /// parse and refuses a value that names no member; whether the category is a category of
    /// audio streams and whether each line can become a stream are rules of the service.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/audio-streams/import")]
    [Authorize(Policy = AuthenticationExtensions.EditAudioStreamsPolicy)]
    [Produces("application/json")]
    public sealed class AudioStreamsImportController : ApiControllerBase
    {
        /// <summary>Form field that carries the uploaded file.</summary>
        private const string PlaylistField = "playlist";

        /// <summary>Form field that asks to append sources to existing streams.</summary>
        private const string AppendSourceField = "appendSourceToExisting";

        /// <summary>Form field that asks to overwrite existing stream settings.</summary>
        private const string OverwriteSettingsField = "overwriteExistingSettings";

        /// <summary>Sentence a type that names no member gets.</summary>
        private const string TypeHint =
            "The stream type must be one of: ondemand, redirect.";

        /// <summary>Sentence a state that names no member gets.</summary>
        private const string StatusHint = "The status must be either running or stopped.";

        /// <summary>Sentence a category that is not an identifier gets.</summary>
        private const string CategoryHint =
            "The category must be the identifier of a category, as a uuid.";

        /// <summary>Sentence a missing or unreadable file gets.</summary>
        private const string PlaylistHint =
            "Choose an M3U playlist file to import.";

        /// <summary>Sentence a checkbox value that is neither true nor false gets.</summary>
        private const string BooleanHint = "That value must be true or false.";

        private readonly IAudioStreamImportService _imports;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioStreamsImportController"/> class.
        /// </summary>
        /// <param name="imports">Service that reads the playlist and writes its entries.</param>
        /// <param name="antiforgery">Antiforgery service the base class checks with.</param>
        /// <param name="logger">Logger the base class reports through.</param>
        public AudioStreamsImportController(
            IAudioStreamImportService imports,
            IAntiforgery antiforgery,
            ILogger<AudioStreamsImportController> logger)
            : base(antiforgery, logger)
        {
            _imports = imports;
        }

        /// <summary>
        /// Reads a playlist and creates every audio stream it can use.
        /// </summary>
        /// <remarks>
        /// A file that is readable answers <c>200</c> even when some entries were skipped or
        /// refused; the counts and the per-line problems state what happened. Only a request
        /// that made no sense - a missing file, a size the server will not read, an HLS
        /// playlist, a file with no stream entry - is a refusal, and the code names which.
        /// </remarks>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        [HttpPost]
        [ProducesResponseType<AudioStreamImportResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status415UnsupportedMediaType)]
        public async Task<IActionResult> ImportStreams(CancellationToken cancellationToken)
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

            // A request already larger than the file limit plus multipart overhead cannot fit
            // the allowed playlist. Refusing it from the header keeps a mistaken upload from
            // being buffered before the file length can be judged.
            if (Request.ContentLength is > AudioStreamImportLimits.MaxFileBytes + 1024 * 1024)
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

            var type = AudioStreamTextParser.ParseTypeOrNull(form["type"].ToString());
            if (type is null)
            {
                errors["type"] = [TypeHint];
            }

            var statusText = form["status"].ToString();
            var status = string.IsNullOrWhiteSpace(statusText)
                ? AudioStreamStatus.Stopped
                : AudioStreamTextParser.ParseStatusOrNull(statusText);

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

            var appendSourceToExisting = ReadBoolean(form, AppendSourceField, errors);
            var overwriteExistingSettings = ReadBoolean(form, OverwriteSettingsField, errors);

            var file = form.Files.GetFile(PlaylistField);
            if (file is null)
            {
                errors[PlaylistField] = [PlaylistHint];
            }

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            if (file!.Length == 0)
            {
                return Failure(
                    StatusCodes.Status400BadRequest,
                    AudioStreamImportResponses.EmptyCode,
                    "The playlist file is empty.");
            }

            if (file.Length > AudioStreamImportLimits.MaxFileBytes)
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
                    AudioStreamImportResponses.UnreadableCode,
                    "The playlist file could not be read.");
            }

            var outcome = await _imports.ImportAsync(
                new NewAudioStreamImport
                {
                    Type = type!.Value,
                    Status = status!.Value,
                    CategoryId = categoryId,
                    Playlist = playlist,
                    RestreamUserAgent = form["restreamUserAgent"].ToString(),
                    RestreamProxy = form["restreamProxy"].ToString(),
                    RestreamCookies = form["restreamCookies"].ToString(),
                    RestreamHeaders = form["restreamHeaders"].ToString(),
                    AppendSourceToExisting = appendSourceToExisting,
                    OverwriteExistingSettings = overwriteExistingSettings
                },
                cancellationToken);

            var (statusCode, body) = AudioStreamImportResponses.Describe(outcome);

            return new ObjectResult(body) { StatusCode = statusCode };
        }

        /// <summary>
        /// Reads one optional checkbox value. Blank or missing means false; only <c>true</c>
        /// and <c>1</c> count as true, anything else is reported on the field.
        /// </summary>
        private static bool ReadBoolean(
            IFormCollection form,
            string field,
            Dictionary<string, string[]> errors)
        {
            var text = form[field].ToString();

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var trimmed = text.Trim();

            if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase)
                || trimmed == "1")
            {
                return true;
            }

            if (string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase)
                || trimmed == "0")
            {
                return false;
            }

            errors[field] = [BooleanHint];
            return false;
        }

        /// <summary>The answer for a file the server will not read.</summary>
        private ObjectResult TooLarge() => Failure(
            StatusCodes.Status413PayloadTooLarge,
            AudioStreamImportResponses.TooLargeCode,
            "The playlist file is larger than "
                + (AudioStreamImportLimits.MaxFileBytes / (1024 * 1024))
                + " MB.");

        /// <summary>Builds one refusal with the protocol's usual body.</summary>
        private static ObjectResult Failure(int status, string code, string message)
            => new(AudioStreamImportResponses.Failure(code, message)) { StatusCode = status };
    }
}
