using Fluxy.API.Contracts.Backups;
using Fluxy.API.Contracts.Common;
using Fluxy.API.Extensions;
using Fluxy.Core.Backups.Enums;
using Fluxy.Core.Backups.Interfaces;
using Fluxy.Core.Backups.Outcomes;
using Fluxy.Core.Backups.ReadModels;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Admin
{
    /// <summary>
    /// The database archives kept on the API host: the list, the create, the restore and the
    /// delete.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The three backup settings are read here and written nowhere.</b> They stay editable only
    /// on <c>/admin/settings</c>, whose <c>PATCH</c> is a full write of the whole row: a second
    /// editing surface on this screen would send seventeen values it never showed and clobber
    /// them. So this collection has no settings route at all, and the screen echoes the three
    /// values read-only with a link to the page that owns them.
    /// </para>
    /// <para>
    /// <b>The two settings grants are reused rather than duplicated.</b> <c>viewSettings</c> admits
    /// the read and <c>editSettings</c> admits every write, so no permission was added to the
    /// catalog, nothing was migrated and a group that may look at the installation's configuration
    /// may look at what it was backed up as. This surface is the settings surface's own safety
    /// net: the grants that open the settings open its backups.
    /// </para>
    /// <para>
    /// <b>There is no metadata row, on purpose.</b> An archive is a file whose name carries the
    /// instant it was taken, so "newest first" is a name order and "is one due" is derived from
    /// the directory. A table beside the files would be a second truth that a restore - which
    /// replaces the database - could roll back into disagreeing with what is on disk.
    /// </para>
    /// <para>
    /// <b>The departure is the restore, and it is the owner's.</b> <c>POST
    /// /admin/backups/{fileName}/restore</c> is not a write to a resource: it is a state transition
    /// of the whole database, and <c>PUT {fileName}</c> meaning "make the database this file" would
    /// dress destruction-and-replacement as an update of an archive that is never a client-editable
    /// resource. The route is the same reasoning family as <c>POST {id}/confirm-registration</c>
    /// under <c>admin/users</c>. Restoring is an accepted short global outage: sessions roll back
    /// to the backup instant and Redis is not restored at all.
    /// </para>
    /// <para>
    /// <b>Every write checks the antiforgery pair before anything else</b> - before the name, the
    /// directory or the tool - because a request without a valid pair is not this application's
    /// request at all.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/backups")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class BackupsController : ApiControllerBase
    {
        /// <summary>Sentence a name that resolves to no archive gets - and one that is not a name.</summary>
        private const string NotFoundMessage =
            "There is no backup file with that name in the backup directory.";

        private readonly IBackupService _backups;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackupsController"/> class.
        /// </summary>
        /// <param name="backups">Service that lists, creates, restores and deletes the archives.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the outcomes are reported to.</param>
        public BackupsController(
            IBackupService backups,
            IAntiforgery antiforgery,
            ILogger<BackupsController> logger)
            : base(antiforgery, logger)
        {
            _backups = backups;
        }

        /// <summary>
        /// Reports every archive in the backup directory, newest first.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with the list, empty when the directory holds nothing.</returns>
        /// <remarks>
        /// There is no pager and no filter: the collection is a directory bounded by the rotation,
        /// and a screen that offered paging over it would be a screen whose second page is
        /// empty by construction.
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewSettingsPolicy)]
        [ProducesResponseType<BackupsListResponse>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetBackups(CancellationToken cancellationToken)
        {
            NoStore();

            var files = await _backups.ListAsync(cancellationToken);

            return Ok(new BackupsListResponse(files.Select(ToResponse).ToList()));
        }

        /// <summary>
        /// Takes a backup now.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 201 <c>backup_created</c> with a <c>Location</c> naming the new file, 409
        /// <c>backup_busy</c>, 500 <c>backup_tool_unavailable</c>, 500 <c>backup_failed</c>, 400
        /// <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// The <c>Location</c> is honest: the file exists under the name it answers with the
        /// moment this returns, and rotation has already run if it had anything to remove.
        /// </remarks>
        [HttpPost]
        [Authorize(Policy = AuthenticationExtensions.EditSettingsPolicy)]
        [ProducesResponseType<BackupActionResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<BackupActionResponse>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<BackupActionResponse>(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateBackup(CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var outcome = await _backups.CreateAsync(cancellationToken);

            if (outcome.Failure == BackupFailure.None)
            {
                Logger.LogInformation(
                    "Created the backup archive {FileName}.",
                    outcome.FileName);

                Response.Headers.Location = $"/admin/backups/{outcome.FileName}";
            }

            return Answer(
                outcome,
                StatusCodes.Status201Created,
                "backup_created",
                "The backup has been created.");
        }

        /// <summary>
        /// Replaces the database with one archive.
        /// </summary>
        /// <param name="fileName">Name of the archive inside the backup directory.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>backup_restored</c>, 404 <c>backup_not_found</c>, 409 <c>backup_busy</c>, 500
        /// <c>backup_tool_unavailable</c>, 500 <c>backup_failed</c>, 400 <c>csrf_invalid</c>
        /// without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>This is the ratified departure.</b> A restore is a state transition of the whole
        /// database, not a write to a resource: the archive is never editable by a client and the
        /// effect is a replacement, so <c>PUT {fileName}</c> would describe an update that does not
        /// exist. <c>POST {fileName}/restore</c> names the act and the thing it is performed on.
        /// </para>
        /// <para>
        /// <b>The interruption is accepted, and it is global.</b> Every connection pool is flushed,
        /// the schema is dropped and recreated under a single transaction, and the pending
        /// migrations then run. Requests served during the run fail; the client that called this
        /// endpoint gets its answer only once the database is current again.
        /// </para>
        /// <para>
        /// <b>Sessions roll back to the backup instant.</b> <c>refresh_tokens</c> is inside the
        /// dump, so everyone who signed in after the backup was taken is effectively signed out -
        /// possibly the operator who pressed the button. Redis is not restored, so revoked-token
        /// identifiers and attempt counters stay where they were. Both facts are documented rather
        /// than mitigated.
        /// </para>
        /// </remarks>
        [HttpPost("{fileName}/restore")]
        [Authorize(Policy = AuthenticationExtensions.EditSettingsPolicy)]
        [ProducesResponseType<BackupActionResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<BackupActionResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<BackupActionResponse>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<BackupActionResponse>(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RestoreBackup(
            [FromRoute] string fileName,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var outcome = await _backups.RestoreAsync(fileName, cancellationToken);

            return Answer(
                outcome,
                StatusCodes.Status200OK,
                "backup_restored",
                "The database has been restored from the backup.");
        }

        /// <summary>
        /// Deletes one archive.
        /// </summary>
        /// <param name="fileName">Name of the archive inside the backup directory.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>backup_deleted</c>, 404 <c>backup_not_found</c>, 409 <c>backup_busy</c>, 500
        /// <c>backup_failed</c>, 400 <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// 200 rather than 204, on the BlacklistEntries precedent: the answer has something to say -
        /// which file went - and a visitor who deleted a row on another tab is told it is already
        /// gone rather than shown a bare success.
        /// </remarks>
        [HttpDelete("{fileName}")]
        [Authorize(Policy = AuthenticationExtensions.EditSettingsPolicy)]
        [ProducesResponseType<BackupActionResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<BackupActionResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<BackupActionResponse>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<BackupActionResponse>(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteBackup(
            [FromRoute] string fileName,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var outcome = await _backups.DeleteAsync(fileName, cancellationToken);

            return Answer(outcome, StatusCodes.Status200OK, "backup_deleted", "The backup has been deleted.");
        }

        /// <summary>
        /// Answers <c>405</c> to the collection methods this resource does not own.
        /// </summary>
        /// <returns>405 <c>method_not_allowed</c>.</returns>
        /// <remarks>
        /// A collection of files is created by <c>POST</c> and read by <c>GET</c>; nothing here
        /// replaces the set, so <c>PUT</c> and <c>PATCH</c> have no meaning. Declared rather than
        /// left to the framework because the contract answers an explicit envelope, and nothing
        /// reads the body.
        /// </remarks>
        [AcceptVerbs("PUT", "PATCH")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status405MethodNotAllowed)]
        public IActionResult RejectCollectionMethod() => MethodNotAllowed();

        /// <summary>
        /// Answers <c>405</c> to the per-file methods this resource does not own.
        /// </summary>
        /// <param name="fileName">Name in the path; deliberately never read.</param>
        /// <returns>405 <c>method_not_allowed</c>.</returns>
        /// <remarks>
        /// <b>The fence is the absence of a handler, and this declares only that absence.</b>
        /// An archive is a file produced by the tool: it is never replaced or edited, and its only
        /// two transitions are the restore and the delete, each with a route of its own. A
        /// <c>{fileName}</c> path names no route for <c>GET</c>/<c>PUT</c>/<c>PATCH</c>, so it is
        /// declared here to answer 405 rather than to fall through to the generic 404. Nothing
        /// reads the identifier or the body.
        /// </remarks>
        [AcceptVerbs("GET", "PUT", "PATCH", Route = "{fileName}")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status405MethodNotAllowed)]
        public IActionResult RejectFileMethod([FromRoute] string fileName)
        {
            _ = fileName;

            return MethodNotAllowed();
        }

        /// <summary>
        /// Answers <c>405</c> to a <c>POST {fileName}</c> that names no transition.
        /// </summary>
        /// <param name="fileName">Name in the path; deliberately never read.</param>
        /// <returns>405 <c>method_not_allowed</c>.</returns>
        /// <remarks>
        /// The restore is the one <c>POST</c> a file admits, and it names itself in the path:
        /// <c>{fileName}/restore</c>. A <c>POST</c> on the bare file would be a create under a name
        /// that already exists, which is not an operation this resource has.
        /// </remarks>
        [HttpPost("{fileName}")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status405MethodNotAllowed)]
        public IActionResult RejectFilePost([FromRoute] string fileName)
        {
            _ = fileName;

            return MethodNotAllowed();
        }

        /// <summary>The one 405 body every fence answers with.</summary>
        private ObjectResult MethodNotAllowed()
            => StatusCode(
                StatusCodes.Status405MethodNotAllowed,
                new MessageResponse
                {
                    Code = "method_not_allowed",
                    Message = "The method is not allowed for this resource."
                });

        /// <summary>
        /// Turns a service outcome into the status and body a client is told.
        /// </summary>
        /// <remarks>
        /// <b>An invalid name and a name that resolves to nothing answer the same 404.</b> The
        /// difference between "that is not a name" and "that name is not here" would let a caller
        /// ask the endpoint which paths this host has, so it is deliberately not observable.
        /// </remarks>
        private IActionResult Answer(
            BackupOutcome outcome,
            int successStatus,
            string successCode,
            string successMessage)
        {
            var (status, code, message) = outcome.Failure switch
            {
                BackupFailure.None => (successStatus, successCode, successMessage),

                BackupFailure.NotFound =>
                    (StatusCodes.Status404NotFound, "backup_not_found", NotFoundMessage),

                BackupFailure.Busy =>
                    (StatusCodes.Status409Conflict,
                        "backup_busy",
                        "Another backup operation is running. Try again in a moment."),

                BackupFailure.ToolUnavailable =>
                    (StatusCodes.Status500InternalServerError,
                        "backup_tool_unavailable",
                        "The backup tool is not available on this server. Docker or the " +
                        "PostgreSQL container could not be reached."),

                BackupFailure.Failed =>
                    (StatusCodes.Status500InternalServerError,
                        "backup_failed",
                        "The backup operation failed. The server log names the reason."),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome.Failure,
                    "Unknown backup outcome.")
            };

            return StatusCode(status, new BackupActionResponse(code, message, outcome.FileName));
        }

        private static BackupFileInfoResponse ToResponse(BackupFileInfo file) => new(
            file.FileName,
            file.SizeBytes,
            file.CreatedAtUtc);
    }
}
