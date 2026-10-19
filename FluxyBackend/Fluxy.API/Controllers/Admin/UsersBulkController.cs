using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Users;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.API.Security;
using Fluxy.Core.Users.Commands;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Interfaces;
using Fluxy.Core.Users.Models;
using Fluxy.Core.Users.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Admin
{
    /// <summary>
    /// The collective operations on the accounts resource, split from
    /// <c>UsersController</c>.
    /// </summary>
    /// <remarks>
    /// The seam is the shape of the request and of the answer, not the number of lines: a bulk
    /// request names a set of rows a table assembled on screen rather than one row with an
    /// address of its own, and it answers with a per-identifier report
    /// (<c>BulkOperationResponse</c>) where the rest of the resource answers with one outcome
    /// for one row. The route is unchanged - <c>POST /admin/users/bulk</c> - and so is every
    /// rule behind it, including the departure the action records for its own shape.
    ///
    /// The class policy is the permission the write beside it carries, because a bulk
    /// operation is one such write applied to several rows at once. The per-target reach of
    /// the caller is still settled row by row in the service, through the grants read here.
    /// </remarks>
    [ApiController]
    [Route("admin/users/bulk")]
    [Authorize(Policy = AuthenticationExtensions.EditAnyUsersPolicy)]
    [Produces("application/json")]
    public sealed class UsersBulkController : ApiControllerBase
    {
        /// <summary>Names the operations a bulk body may ask for, for the sentence that lists them.</summary>
        private const string BulkActionHint =
            "The action must be 'block', 'unblock', 'delete', 'confirm-registration' " +
            "or 'assign-group'.";

        /// <summary>Sentence a group that is not an identifier gets.</summary>
        private const string GroupIdHint =
            "The group must be the identifier of a group, as a uuid.";

        /// <summary>Sentence a set of identifiers that is not all uuids gets.</summary>
        private const string IdsHint = "Every identifier must be the uuid of an account.";

        private readonly IUserBulkService _users;

        /// <summary>
        /// Reader of this request's account, shared with the authorization handlers that ran
        /// on their way into the action.
        /// </summary>
        private readonly RequestStandingReader _standing;

        /// <summary>
        /// Initializes a new instance of the <see cref="UsersBulkController"/> class.
        /// </summary>
        /// <param name="users">Service that applies the operation to the named accounts.</param>
        /// <param name="standing">Reader of the account behind the request, for the permissions its group holds.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public UsersBulkController(
            IUserBulkService users,
            RequestStandingReader standing,
            IAntiforgery antiforgery,
            ILogger<UsersBulkController> logger)
            : base(antiforgery, logger)
        {
            _users = users;
            _standing = standing;
        }

        /// <summary>
        /// Applies one operation to every account the request names, and reports how each one
        /// ended.
        /// </summary>
        /// <param name="request">Which operation, over which accounts, and where to move them.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 with one entry per identifier, 400 when the body broke a rule on itself (an
        /// unknown operation, no identifiers, more identifiers than one page holds, or a move
        /// that names no group), 403 from the coarse policy on the way in, and 400
        /// <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>A recorded departure from the REST shape of the rest of this controller</b>, and
        /// the same one the session surface records for <c>/auth/refresh</c>: the path carries
        /// a verb because the resource it would otherwise name does not exist. "These twenty
        /// accounts" is a set the table on screen assembled in the browser, not a row with an
        /// address of its own, so there is no noun to put after the slash - and both
        /// alternatives were worse: five routes (<c>bulk/block</c>, <c>bulk/delete</c>, ...)
        /// would repeat the antiforgery check, the permission policy and the identifier
        /// parsing five times over, while a client wanting six operations would have to know
        /// which five exist.
        /// </para>
        /// <para>
        /// <b>The 200 describes the run, not every row in it.</b> What happened to a particular
        /// account is stated in its own entry with the same code the one-row endpoint gives,
        /// because the refusals that protect the caller are about that account and about
        /// nothing else: an operator who selected their own row among twenty is told so, and
        /// the other nineteen are still blocked. A status code can only describe the request as
        /// a whole, and the request as a whole *was* answered - every identifier it named came
        /// back with a verdict. Filling that one code with the worst verdict in the set would
        /// make "nine blocked, one was mine" indistinguishable from "nothing happened", and a
        /// client would be unable to redraw the list without guessing.
        /// </para>
        /// <para>
        /// No captcha and no attempt window, for the reason the rest of this controller gives:
        /// both exist to guard a request with no session at all, and everything here is behind
        /// an admin session, the edit permission and a fresh antiforgery pair. The identifiers
        /// are capped at the size of one page rather than accepted without bound, so the work
        /// one request can cause is the work one page of rows can cause.
        /// </para>
        /// </remarks>
        [HttpPost]
        [Authorize(Policy = AuthenticationExtensions.EditAnyUsersPolicy)]
        [ProducesResponseType<BulkOperationResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> BulkUsers(
            [FromBody] BulkUsersRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (User.UserId() is not { } actingUserId)
            {
                return ApiResults.Anonymous();
            }

            // Everything the body can be wrong about is read first, so one answer names every
            // mistake rather than one per round trip - and so that no row is touched by a
            // request that is about to be refused for another reason.
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var action = ParseBulkAction(request.Action);
            if (action is null)
            {
                errors[nameof(BulkUsersRequest.Action)] = [BulkActionHint];
            }

            var ids = ParseIds(request.Ids, errors);

            Guid? groupId = null;

            if (string.IsNullOrWhiteSpace(request.GroupId))
            {
                // Absent is fine for four of the five operations and is not fine for the one
                // that has somewhere to go. Refused here rather than by treating an absent
                // group as "leave it alone", which is what a patch means by absent - an
                // operation that answered success over twenty accounts and moved none of them
                // would be the worst thing this endpoint could say.
                if (action is BulkUserAction.AssignGroup)
                {
                    errors[nameof(BulkUsersRequest.GroupId)] = [GroupIdHint];
                }
            }
            else if (Guid.TryParse(request.GroupId, out var parsedGroup))
            {
                groupId = parsedGroup;
            }
            else
            {
                errors[nameof(BulkUsersRequest.GroupId)] = [GroupIdHint];
            }

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            return Ok(BulkResponses.Describe(await _users.BulkAsync(
                new BulkUserOperation
                {
                    Action = action!.Value,
                    UserIds = ids!,
                    GroupId = groupId
                },
                actingUserId,
                await _standing.ReadGrantsAsync(User),
                cancellationToken)));
        }

        /// <summary>
        /// Turns the text a bulk body sent into the operation it names, or nothing at all.
        /// </summary>
        /// <remarks>
        /// Compared after lowering and with the hyphens spelled the way the rest of this API
        /// spells its actions, so <c>Block</c> and <c>confirm-registration</c> are both the
        /// request a client naturally sends. Enum binding is deliberately not used: it takes a
        /// member name spelled in full, so the conventional <c>block</c> would be refused while
        /// <c>Block</c> was accepted - the exact lesson <c>sortOrder</c> taught this API.
        /// </remarks>
        private static BulkUserAction? ParseBulkAction(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "block" => BulkUserAction.Block,
                "unblock" => BulkUserAction.Unblock,
                "delete" => BulkUserAction.Delete,
                "confirm-registration" => BulkUserAction.ConfirmRegistration,
                "assign-group" => BulkUserAction.AssignGroup,
                _ => null
            };

        /// <summary>
        /// Turns the identifiers a bulk body sent into the rows they name, or nothing at all
        /// once one of them is wrong.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The three refusals answer different mistakes and none of them is clamped. A list
        /// nobody filled in is a request that named nothing, which is refused rather than
        /// answered with an empty run - an operation over zero accounts is a success that
        /// changed nothing, and a client that meant to send twenty deserves to be told its
        /// body did not arrive.
        /// </para>
        /// <para>
        /// <b>Duplicates collapse rather than being refused.</b> A page of a table cannot hold
        /// the same row twice, so a duplicate can only come from a client assembling the list
        /// by hand - and running an operation twice over one row is not what anybody means by
        /// selecting it. Refusing would turn a harmless overlap into a 400 the operator could
        /// not explain.
        /// </para>
        /// </remarks>
        private static IReadOnlyList<Guid>? ParseIds(
            IReadOnlyList<string>? ids,
            Dictionary<string, string[]> errors)
        {
            if (ids is null || ids.Count is 0)
            {
                errors[nameof(BulkUsersRequest.Ids)] = ["Select at least one account."];
                return null;
            }

            if (ids.Count > BulkOperationLimits.MaxItems)
            {
                errors[nameof(BulkUsersRequest.Ids)] =
                [$"At most {BulkOperationLimits.MaxItems} accounts may be named in one request."];
                return null;
            }

            var parsed = new List<Guid>(ids.Count);

            foreach (var text in ids)
            {
                if (!Guid.TryParse(text, out var id))
                {
                    errors[nameof(BulkUsersRequest.Ids)] = [IdsHint];
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
