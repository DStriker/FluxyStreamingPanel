using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Transcoding;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Transcoding.Commands;
using Fluxy.Core.Transcoding.Enums;
using Fluxy.Core.Transcoding.Interfaces;
using Fluxy.Core.Transcoding.Outcomes;
using Fluxy.Core.Transcoding.ReadModels;
using Fluxy.Core.Transcoding.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Admin
{
    /// <summary>
    /// Managing the transcoding profiles: one page of the table, one profile in full, the
    /// picker the four content forms draw, and the writes that create, change and delete them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two policies on every action: the class carries <c>AdminOnly</c> the way the other admin
    /// controllers do, and each action adds its own permission on top. Read and write are
    /// separate grants, so a group which may look at the profiles and a group which may change
    /// them stay two different things. The <c>options</c> action is the deliberate exception:
    /// it is a field of a content form, so it is gated by any of the four content-view grants
    /// instead of the profile pair.
    /// </para>
    /// <para>
    /// <b>No captcha and no attempt window</b>, for the reason the rest of the admin area gives:
    /// both guard a request with no session at all, and everything here is behind an admin
    /// session and a fresh antiforgery pair, which <see cref="ApiControllerBase.RejectsCsrfAsync"/>
    /// checks before any body is read.
    /// </para>
    /// <para>
    /// <b>The enum fields travel as ffmpeg's own lowercase names.</b> The controller parses them
    /// through <see cref="TranscodingProfileTextParser"/>, which owns the spelling of every
    /// member in both directions, and refuses a value that names no member under the field it
    /// came in.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/transcoding-profiles")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class TranscodingProfilesController : ApiControllerBase
    {
        /// <summary>Sentence a column that names nothing gets.</summary>
        private const string SortByHint = "The sort field must be: name.";

        /// <summary>Sentence a direction that names nothing gets.</summary>
        private const string SortOrderHint = "The sort order must be either asc or desc.";

        private readonly ITranscodingProfileService _profiles;

        /// <summary>
        /// Initializes a new instance of the <see cref="TranscodingProfilesController"/> class.
        /// </summary>
        /// <param name="profiles">Service that reads and changes the profiles.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public TranscodingProfilesController(
            ITranscodingProfileService profiles,
            IAntiforgery antiforgery,
            ILogger<TranscodingProfilesController> logger)
            : base(antiforgery, logger)
        {
            _profiles = profiles;
        }

        /// <summary>
        /// Reports one page of the table, filtered and ordered the way the query asked.
        /// </summary>
        /// <remarks>
        /// Search, order and paging live here rather than in the browser because the pager
        /// shows a <c>total</c>. The bounds are refused rather than clamped, and a value that
        /// names nothing is refused rather than treated as "no filter".
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewTranscodingProfilesPolicy)]
        [ProducesResponseType<AdminTranscodingProfilePageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetProfiles(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = TranscodingProfileListLimits.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = "name",
            [FromQuery] string? sortOrder = "asc",
            CancellationToken cancellationToken = default)
        {
            NoStore();

            var field = ParseSortField(sortBy);
            var order = TranscodingProfileTextParserOrder(sortOrder);

            if (page < 1
                || pageSize is < 1 or > TranscodingProfileListLimits.MaxPageSize
                || (search?.Length ?? 0) > TranscodingProfileListLimits.MaxSearchLength
                || field is null
                || order is null)
            {
                return ApiResults.ValidationFailed(
                    ListErrors(page, pageSize, search, field, order));
            }

            var profiles = await _profiles.GetPageAsync(
                page,
                pageSize,
                search,
                field.Value,
                order.Value,
                cancellationToken);

            return Ok(new AdminTranscodingProfilePageResponse
            {
                Items = profiles.Items.Select(ToResponse).ToList(),
                Total = profiles.Total,
                Page = profiles.Page,
                PageSize = profiles.PageSize
            });
        }

        /// <summary>
        /// Reports every profile as a picker entry - what the four content forms draw.
        /// </summary>
        /// <remarks>
        /// A read of the same rows behind a different grant: choosing a profile while editing a
        /// stream is part of editing the stream, so the four content-view grants admit it and
        /// the profile pair is not required. The answer is a bare array rather than a page,
        /// because the picker needs every profile and there are tens of them, not thousands.
        /// </remarks>
        [HttpGet("options")]
        [Authorize(Policy = AuthenticationExtensions.ViewTranscodingProfileOptionsPolicy)]
        [ProducesResponseType<IReadOnlyList<AdminTranscodingProfileOptionResponse>>(
            StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOptions(CancellationToken cancellationToken)
        {
            NoStore();

            var options = await _profiles.GetOptionsAsync(cancellationToken);

            return Ok(options.Select(option => new AdminTranscodingProfileOptionResponse
            {
                Id = option.Id.ToString(),
                Name = option.Name
            }).ToList());
        }

        /// <summary>Reports one profile, for the edit form.</summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.ViewTranscodingProfilesPolicy)]
        [ProducesResponseType<AdminTranscodingProfileResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProfile(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            NoStore();

            var profile = await _profiles.GetAsync(id, cancellationToken);

            return profile is null
                ? Answer(new TranscodingProfileOutcome
                {
                    Action = TranscodingProfileAction.NotFound,
                    ProfileId = id
                })
                : Ok(ToResponse(profile));
        }

        /// <summary>Creates a profile.</summary>
        /// <remarks>
        /// The 201 carries a <c>Location</c>, and it is honest: the row exists in full the
        /// moment it is written and this very controller serves the address it names.
        /// </remarks>
        [HttpPost]
        [Authorize(Policy = AuthenticationExtensions.EditTranscodingProfilesPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateProfile(
            [FromBody] CreateTranscodingProfileRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var videoCodec = TranscodingProfileTextParser.ParseVideoCodec(request.VideoCodec);
            var audioCodec = TranscodingProfileTextParser.ParseAudioCodec(request.AudioCodec);
            var preset = TranscodingProfileTextParser.ParsePreset(request.Preset);
            var videoProfile = TranscodingProfileTextParser.ParseVideoProfile(request.VideoProfile);

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            EnumErrors(
                errors,
                request.VideoCodec,
                videoCodec is null,
                nameof(CreateTranscodingProfileRequest.VideoCodec),
                TranscodingProfileTextParser.VideoCodecHint);
            EnumErrors(
                errors,
                request.AudioCodec,
                audioCodec is null,
                nameof(CreateTranscodingProfileRequest.AudioCodec),
                TranscodingProfileTextParser.AudioCodecHint);
            EnumErrors(
                errors,
                request.Preset,
                preset is null,
                nameof(CreateTranscodingProfileRequest.Preset),
                TranscodingProfileTextParser.PresetHint);
            EnumErrors(
                errors,
                request.VideoProfile,
                videoProfile is null,
                nameof(CreateTranscodingProfileRequest.VideoProfile),
                TranscodingProfileTextParser.VideoProfileHint);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _profiles.CreateAsync(
                new NewTranscodingProfile
                {
                    Name = request.Name ?? string.Empty,
                    VideoCodec = videoCodec,
                    AudioCodec = audioCodec,
                    Preset = preset,
                    VideoProfile = videoProfile,
                    VideoBitrateAvg = request.VideoBitrateAvg,
                    AudioBitrateAvg = request.AudioBitrateAvg,
                    MinBitrateTolerance = request.MinBitrateTolerance,
                    MaxBitrateTolerance = request.MaxBitrateTolerance,
                    BufferSize = request.BufferSize,
                    Crf = request.Crf,
                    Width = request.Width,
                    Height = request.Height,
                    AspectRatio = request.AspectRatio,
                    Framerate = request.Framerate,
                    AudioSampleRate = request.AudioSampleRate,
                    AudioChannels = request.AudioChannels,
                    Threads = request.Threads,
                    LogoUrl = request.LogoUrl,
                    RedactionX = request.RedactionX,
                    RedactionY = request.RedactionY,
                    RedactionWidth = request.RedactionWidth,
                    RedactionHeight = request.RedactionHeight
                },
                cancellationToken);

            var (statusCode, body) = TranscodingProfileResponses.Describe(outcome);

            if (statusCode == StatusCodes.Status201Created)
            {
                Response.Headers.Location = $"/admin/transcoding-profiles/{outcome.ProfileId}";
            }

            return StatusCode(statusCode, body);
        }

        /// <summary>
        /// Changes the options of a profile. A body that names nothing is a success rather than
        /// a complaint - the convention every other PATCH on this API follows.
        /// </summary>
        [HttpPatch("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditTranscodingProfilesPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateProfile(
            [FromRoute] Guid id,
            [FromBody] UpdateTranscodingProfileRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var videoCodec = TranscodingProfileTextParser.ParseVideoCodec(request.VideoCodec);
            var audioCodec = TranscodingProfileTextParser.ParseAudioCodec(request.AudioCodec);
            var preset = TranscodingProfileTextParser.ParsePreset(request.Preset);
            var videoProfile = TranscodingProfileTextParser.ParseVideoProfile(request.VideoProfile);

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            EnumErrors(
                errors,
                request.VideoCodec,
                videoCodec is null,
                nameof(UpdateTranscodingProfileRequest.VideoCodec),
                TranscodingProfileTextParser.VideoCodecHint);
            EnumErrors(
                errors,
                request.AudioCodec,
                audioCodec is null,
                nameof(UpdateTranscodingProfileRequest.AudioCodec),
                TranscodingProfileTextParser.AudioCodecHint);
            EnumErrors(
                errors,
                request.Preset,
                preset is null,
                nameof(UpdateTranscodingProfileRequest.Preset),
                TranscodingProfileTextParser.PresetHint);
            EnumErrors(
                errors,
                request.VideoProfile,
                videoProfile is null,
                nameof(UpdateTranscodingProfileRequest.VideoProfile),
                TranscodingProfileTextParser.VideoProfileHint);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            return Answer(await _profiles.UpdateAsync(
                id,
                new TranscodingProfilePatch
                {
                    Name = request.Name,
                    VideoCodec = videoCodec,
                    AudioCodec = audioCodec,
                    Preset = preset,
                    VideoProfile = videoProfile,
                    VideoBitrateAvg = request.VideoBitrateAvg,
                    AudioBitrateAvg = request.AudioBitrateAvg,
                    MinBitrateTolerance = request.MinBitrateTolerance,
                    MaxBitrateTolerance = request.MaxBitrateTolerance,
                    BufferSize = request.BufferSize,
                    Crf = request.Crf,
                    Width = request.Width,
                    Height = request.Height,
                    AspectRatio = request.AspectRatio,
                    Framerate = request.Framerate,
                    AudioSampleRate = request.AudioSampleRate,
                    AudioChannels = request.AudioChannels,
                    Threads = request.Threads,
                    LogoUrl = request.LogoUrl,
                    RedactionX = request.RedactionX,
                    RedactionY = request.RedactionY,
                    RedactionWidth = request.RedactionWidth,
                    RedactionHeight = request.RedactionHeight
                },
                cancellationToken));
        }

        /// <summary>Deletes a profile nothing references.</summary>
        /// <remarks>
        /// A <c>DELETE</c> that answers 200 rather than 204, because it has something to say:
        /// the difference between a profile that was deleted and one that was not there is the
        /// whole answer, and a profile still referenced by a stream answers a 409 that names
        /// the refusal instead of breaking the stream.
        /// </remarks>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditTranscodingProfilesPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteProfile(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await _profiles.DeleteAsync(id, cancellationToken));
        }

        /// <summary>Turns the column text into the member it names, or null.</summary>
        private static TranscodingProfileSortField? ParseSortField(string? value)
            => value?.Trim().ToLowerInvariant() switch
            {
                "name" => TranscodingProfileSortField.Name,
                _ => null
            };

        /// <summary>Turns <c>asc</c> / <c>desc</c> into the table's order.</summary>
        private static TranscodingProfileSortOrder? TranscodingProfileTextParserOrder(string? value)
            => SortOrderParser.Parse(
                value,
                TranscodingProfileSortOrder.Ascending,
                TranscodingProfileSortOrder.Descending);

        /// <summary>
        /// Which of the bounds a list request broke, keyed by the camelCase name of the query
        /// parameter so the reason lands under the input a client would fix.
        /// </summary>
        private static Dictionary<string, string[]> ListErrors(
            int page,
            int pageSize,
            string? search,
            TranscodingProfileSortField? field,
            TranscodingProfileSortOrder? order)
        {
            var errors = ListQueryErrors.Common(
                page,
                pageSize,
                search,
                TranscodingProfileListLimits.MaxPageSize,
                TranscodingProfileListLimits.MaxSearchLength);

            if (field is null)
            {
                errors["sortBy"] = [SortByHint];
            }

            if (order is null)
            {
                errors["sortOrder"] = [SortOrderHint];
            }

            return errors;
        }

        private static void EnumErrors(
            Dictionary<string, string[]> errors,
            string? sent,
            bool unparsed,
            string field,
            string hint)
        {
            if (unparsed && !string.IsNullOrWhiteSpace(sent))
            {
                errors[field] = [hint];
            }
        }

        /// <summary>
        /// Turns the service's answer into the status and body a client is told, the one place
        /// where a <see cref="TranscodingProfileAction"/> becomes a number.
        /// </summary>
        private static IActionResult Answer(TranscodingProfileOutcome outcome)
        {
            var (status, body) = TranscodingProfileResponses.Describe(outcome);

            return new ObjectResult(body) { StatusCode = status };
        }

        /// <summary>Turns a read model into the shape the API answers with.</summary>
        private static AdminTranscodingProfileResponse ToResponse(TranscodingProfileView profile) => new()
        {
            Id = profile.Id.ToString(),
            Name = profile.Name,
            VideoCodec = TranscodingProfileTextParser.NameOf(profile.VideoCodec),
            AudioCodec = TranscodingProfileTextParser.NameOf(profile.AudioCodec),
            Preset = TranscodingProfileTextParser.NameOf(profile.Preset),
            VideoProfile = TranscodingProfileTextParser.NameOf(profile.VideoProfile),
            VideoBitrateAvg = profile.VideoBitrateAvg,
            AudioBitrateAvg = profile.AudioBitrateAvg,
            MinBitrateTolerance = profile.MinBitrateTolerance,
            MaxBitrateTolerance = profile.MaxBitrateTolerance,
            BufferSize = profile.BufferSize,
            Crf = profile.Crf,
            Width = profile.Width,
            Height = profile.Height,
            AspectRatio = profile.AspectRatio,
            Framerate = profile.Framerate,
            AudioSampleRate = profile.AudioSampleRate,
            AudioChannels = profile.AudioChannels,
            Threads = profile.Threads,
            LogoUrl = profile.LogoUrl,
            RedactionX = profile.RedactionX,
            RedactionY = profile.RedactionY,
            RedactionWidth = profile.RedactionWidth,
            RedactionHeight = profile.RedactionHeight
        };
    }
}
