using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Settings;
using Fluxy.API.Extensions;
using Fluxy.Core.Settings.Commands;
using Fluxy.Core.Settings.Interfaces;
using Fluxy.Core.Settings.ReadModels;
using Fluxy.Core.Settings.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Admin
{
    /// <summary>
    /// The MAG portal settings: one row an operator reads and writes in full.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The shape is <c>SettingsController</c>'s: the class carries <c>AdminOnly</c>, each action
    /// adds its own permission, there is no <c>POST</c>/<c>PUT</c>/<c>DELETE</c>/<c>{id}</c>, and
    /// every method the collection does not own is answered <c>405 method_not_allowed</c> by the
    /// framework's status-page envelope.
    /// </para>
    /// <para>
    /// Both enum fields travel as the owner's own words - <c>mpeg-ts</c>/<c>mpegts</c>/<c>hls</c>
    /// and <c>default</c>/<c>digital</c>/<c>emerald</c>/<c>cappucino</c>/<c>ocean blue</c> - and
    /// are parsed through the Core catalogues, so the parser and the answering spelling cannot
    /// disagree. Nothing in this pass produces a MAG portal: the row is stored and exposed.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/mag-settings")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class MagSettingsController : ApiControllerBase
    {
        private readonly IMagSettingsService _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="MagSettingsController"/> class.
        /// </summary>
        /// <param name="settings">Service that reads and writes the row.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public MagSettingsController(
            IMagSettingsService settings,
            IAntiforgery antiforgery,
            ILogger<MagSettingsController> logger)
            : base(antiforgery, logger)
        {
            _settings = settings;
        }

        /// <summary>Reports the MAG settings.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with every stored value, both enums as the owner's words.</returns>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewSettingsPolicy)]
        [ProducesResponseType<MagSettingsResponse>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
        {
            NoStore();

            return Ok(ToResponse(await _settings.GetAsync(cancellationToken)));
        }

        /// <summary>
        /// Writes the whole MAG settings row.
        /// </summary>
        /// <param name="request">Every value the row must carry.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>mag_settings_updated</c>, 400 <c>validation_failed</c> with the rejected
        /// fields, 400 <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// A full write: all eleven values are required. A container or theme that names no
        /// member is refused under its own field, and a model list is refused when it is empty or
        /// names a model outside the catalogue - it is never silently dropped.
        /// </remarks>
        [HttpPatch]
        [Authorize(Policy = AuthenticationExtensions.EditSettingsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateSettings(
            [FromBody] UpdateMagSettingsRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var (status, body) = MagSettingsResponses.Describe(await _settings.UpdateAsync(
                new MagSettingsPatch
                {
                    ShowAllCategories = request.ShowAllCategories ?? false,
                    StrictSecurity = request.StrictSecurity ?? false,
                    ShowChannelLogos = request.ShowChannelLogos ?? false,
                    ShowPreviewLogos = request.ShowPreviewLogos ?? false,
                    DefaultContainer = request.DefaultContainer,
                    DefaultTheme = request.DefaultTheme,
                    RecordingMaxLengthMinutes = request.RecordingMaxLengthMinutes ?? int.MinValue,
                    MaxLocalRecordings = request.MaxLocalRecordings ?? int.MinValue,
                    PlaybackLimit = request.PlaybackLimit ?? int.MinValue,
                    AllowedStbTypes = request.AllowedStbTypes ?? [],
                    AllowedRecordingStbTypes = request.AllowedRecordingStbTypes ?? []
                },
                cancellationToken));

            return StatusCode(status, body);
        }

        /// <summary>
        /// Answers <c>405</c> to the per-row methods this resource does not own.
        /// </summary>
        /// <param name="id">Identifier in the path; deliberately never read.</param>
        /// <returns>405 <c>method_not_allowed</c>.</returns>
        /// <remarks>
        /// The fence <c>SettingsController</c> declares, restated for this row: there is one MAG
        /// settings row and no identifier a client chooses, so a <c>{id}</c> path names no route
        /// and is answered 405 rather than the 404 an unknown path would otherwise produce.
        /// </remarks>
        [AcceptVerbs("POST", "PUT", "DELETE", Route = "{id}")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status405MethodNotAllowed)]
        public IActionResult RejectRowMethod([FromRoute] string id)
        {
            _ = id;

            return StatusCode(
                StatusCodes.Status405MethodNotAllowed,
                new MessageResponse
                {
                    Code = "method_not_allowed",
                    Message = "The method is not allowed for this resource."
                });
        }

        /// <summary>Turns the stored snapshot into the shape the API answers with.</summary>
        private static MagSettingsResponse ToResponse(MagSettingsSnapshot settings) => new()
        {
            ShowAllCategories = settings.ShowAllCategories,
            StrictSecurity = settings.StrictSecurity,
            ShowChannelLogos = settings.ShowChannelLogos,
            ShowPreviewLogos = settings.ShowPreviewLogos,
            DefaultContainer = MagContainerCatalog.NameOf(settings.DefaultContainer),
            DefaultTheme = MagThemeCatalog.NameOf(settings.DefaultTheme),
            RecordingMaxLengthMinutes = settings.RecordingMaxLengthMinutes,
            MaxLocalRecordings = settings.MaxLocalRecordings,
            PlaybackLimit = settings.PlaybackLimit,
            AllowedStbTypes = settings.AllowedStbTypes,
            AllowedRecordingStbTypes = settings.AllowedRecordingStbTypes
        };
    }
}
