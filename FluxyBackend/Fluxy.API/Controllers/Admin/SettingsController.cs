using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Settings;
using Fluxy.API.Extensions;
using Fluxy.Application.Settings.Utils;
using Fluxy.Core.Settings.Commands;
using Fluxy.Core.Settings.Interfaces;
using Fluxy.Core.Settings.ReadModels;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Admin
{
    /// <summary>
    /// The installation-wide settings: one row an operator reads and writes in full.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two policies on every action: the class carries <c>AdminOnly</c> the way the other admin
    /// controllers do, and each action adds its own permission. Read and write are separate
    /// grants, so a group that may look at the settings and a group that may change them stay two
    /// different things.
    /// </para>
    /// <para>
    /// <b>There is no <c>POST</c>, <c>PUT</c>, <c>DELETE</c> or <c>{id}</c> path.</b> The
    /// resource is a single row with no identifier a client chooses and no lifecycle: it is
    /// created by the migration, kept by the seeder, and written only by <c>PATCH</c>. Every
    /// method this collection does not own is answered <c>405 method_not_allowed</c> by the
    /// framework's status-page envelope, and that is the fence - an operator cannot create a
    /// second settings row or delete the installation's.
    /// </para>
    /// <para>
    /// <b>The response never carries the private reCAPTCHA key.</b> It answers
    /// <c>recaptchaConfigured</c> instead, and the patch's <c>recaptchaSecretKey</c> is the one
    /// optional field: absent or empty keeps the stored secret, a value replaces it.
    /// </para>
    /// <para>
    /// No captcha and no attempt window, for the reason the rest of the admin area gives: both
    /// guard a request with no session, and everything here is behind an admin session and a
    /// fresh antiforgery pair, which <see cref="ApiControllerBase.RejectsCsrfAsync"/> checks
    /// before any body is read.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/settings")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class SettingsController : ApiControllerBase
    {
        private readonly ISettingsService _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsController"/> class.
        /// </summary>
        /// <param name="settings">Service that reads and writes the row.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public SettingsController(
            ISettingsService settings,
            IAntiforgery antiforgery,
            ILogger<SettingsController> logger)
            : base(antiforgery, logger)
        {
            _settings = settings;
        }

        /// <summary>Reports the installation settings, without the private reCAPTCHA key.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with every stored value; <c>recaptchaConfigured</c> in place of the key.</returns>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewSettingsPolicy)]
        [ProducesResponseType<ApplicationSettingsResponse>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
        {
            NoStore();

            return Ok(ToResponse(await _settings.GetAsync(cancellationToken)));
        }

        /// <summary>
        /// Writes the whole settings row.
        /// </summary>
        /// <param name="request">Every value the row must carry.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>settings_updated</c>, 400 <c>validation_failed</c> with the rejected fields,
        /// 400 <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// <b>The patch is the whole row.</b> Every other value is required and replaces the
        /// stored one; an omitted text is refused as the blank it becomes, an omitted boolean is
        /// <c>false</c>, an omitted number is its zero, and an absent or unknown
        /// <c>backupPeriod</c> is a refusal under that field.
        /// <c>recaptchaSecretKey</c> is the exception: absent or empty keeps what the row holds.
        /// </remarks>
        [HttpPatch]
        [Authorize(Policy = AuthenticationExtensions.EditSettingsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateSettings(
            [FromBody] UpdateApplicationSettingsRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var (status, body) = SettingsResponses.Describe(await _settings.UpdateAsync(
                new ApplicationSettingsPatch
                {
                    ServiceName = request.ServiceName ?? string.Empty,
                    DefaultTimeZone = request.DefaultTimeZone ?? string.Empty,
                    Enigma2BouquetName = request.Enigma2BouquetName ?? string.Empty,
                    RecaptchaSiteKey = request.RecaptchaSiteKey ?? string.Empty,
                    RecaptchaSecretKey = request.RecaptchaSecretKey,
                    EnableTrials = request.EnableTrials ?? false,
                    PublicApiFloodLimit = request.PublicApiFloodLimit ?? int.MinValue,
                    PublicApiFloodWhitelist = request.PublicApiFloodWhitelist ?? [],
                    RequireUserAgent = request.RequireUserAgent ?? false,
                    RetentionUserLogs = request.RetentionUserLogs ?? int.MinValue,
                    RetentionCreditLogs = request.RetentionCreditLogs ?? int.MinValue,
                    RetentionConnectionLogs = request.RetentionConnectionLogs ?? int.MinValue,
                    RetentionApiRequestFailures =
                        request.RetentionApiRequestFailures ?? int.MinValue,
                    RetentionStreamProblems = request.RetentionStreamProblems ?? int.MinValue,
                    RetentionMagEvents = request.RetentionMagEvents ?? int.MinValue,
                    BackupEnabled = request.BackupEnabled ?? false,
                    BackupPeriod = request.BackupPeriod,
                    BackupRotationCount = request.BackupRotationCount ?? int.MinValue,
                    FallbackStreamDownUrl = request.FallbackStreamDownUrl ?? string.Empty,
                    FallbackUserBannedUrl = request.FallbackUserBannedUrl ?? string.Empty,
                    FallbackUserExpiredUrl = request.FallbackUserExpiredUrl ?? string.Empty,
                    FallbackMaxConnectionsUrl = request.FallbackMaxConnectionsUrl ?? string.Empty
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
        /// <b>The fence is the absence of a settings row with an identifier.</b> The table holds
        /// the installation's one row, so there is nothing to create, replace or delete: every
        /// method the collection does not own is answered by the framework from the collection
        /// route, and a <c>{id}</c> path names no route at all - so it is declared here to answer
        /// 405 rather than the 404 an unknown path would otherwise produce. Nothing reads the
        /// body or the identifier.
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
        /// <remarks>
        /// The private key is dropped here and nowhere else: this is the only mapping that
        /// reaches a client, and <see cref="ApplicationSettingsSnapshot.RecaptchaConfigured"/> is
        /// what takes its place.
        /// </remarks>
        private static ApplicationSettingsResponse ToResponse(ApplicationSettingsSnapshot settings)
            => new()
            {
                ServiceName = settings.ServiceName,
                DefaultTimeZone = settings.DefaultTimeZone,
                Enigma2BouquetName = settings.Enigma2BouquetName,
                RecaptchaSiteKey = settings.RecaptchaSiteKey,
                RecaptchaConfigured = settings.RecaptchaConfigured,
                EnableTrials = settings.EnableTrials,
                PublicApiFloodLimit = settings.PublicApiFloodLimit,
                PublicApiFloodWhitelist = settings.PublicApiFloodWhitelist,
                RequireUserAgent = settings.RequireUserAgent,
                RetentionUserLogs = settings.RetentionUserLogs,
                RetentionCreditLogs = settings.RetentionCreditLogs,
                RetentionConnectionLogs = settings.RetentionConnectionLogs,
                RetentionApiRequestFailures = settings.RetentionApiRequestFailures,
                RetentionStreamProblems = settings.RetentionStreamProblems,
                RetentionMagEvents = settings.RetentionMagEvents,
                BackupEnabled = settings.BackupEnabled,
                BackupPeriod = SettingsRules.NameOf(settings.BackupPeriod),
                BackupRotationCount = settings.BackupRotationCount,
                FallbackStreamDownUrl = settings.FallbackStreamDownUrl,
                FallbackUserBannedUrl = settings.FallbackUserBannedUrl,
                FallbackUserExpiredUrl = settings.FallbackUserExpiredUrl,
                FallbackMaxConnectionsUrl = settings.FallbackMaxConnectionsUrl
            };
    }
}
