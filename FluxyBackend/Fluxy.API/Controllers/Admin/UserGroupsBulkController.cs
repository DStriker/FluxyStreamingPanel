using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.UserGroups;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Users.Commands;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Interfaces;
using Fluxy.Core.Users.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Admin
{
    /// <summary>
    /// The collective operations on the groups resource, split from
    /// <c>UserGroupsController</c>.
    /// </summary>
    /// <remarks>
    /// The seam is the shape of the request and of the answer, not the number of lines: a bulk
    /// request names a set of rows a table assembled on screen rather than one row with an
    /// address of its own, and it answers with a per-identifier report
    /// (<c>BulkOperationResponse</c>) where the rest of the resource answers with one outcome
    /// for one row. The route is unchanged - <c>POST /admin/user-groups/bulk</c> - and so is
    /// every rule behind it.
    ///
    /// The class policy is the permission the single write beside it carries, because a bulk
    /// operation is one of those writes applied to several rows at once.
    /// </remarks>
    [ApiController]
    [Route("admin/user-groups/bulk")]
    [Authorize(Policy = AuthenticationExtensions.EditUserGroupsPolicy)]
    [Produces("application/json")]
    public sealed class UserGroupsBulkController : ApiControllerBase
    {
        /// <summary>Names the operations a bulk body may ask for, for the sentence that lists them.</summary>
        private const string BulkActionHint =
            "The action must be 'block', 'unblock' or 'delete'.";

        /// <summary>Sentence a set of identifiers that is not all uuids gets.</summary>
        private const string IdsHint = "Every identifier must be the uuid of a group.";

        private readonly IUserGroupService _groups;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserGroupsBulkController"/> class.
        /// </summary>
        /// <param name="groups">Service that applies the operation to the named rows.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public UserGroupsBulkController(
            IUserGroupService groups,
            IAntiforgery antiforgery,
            ILogger<UserGroupsBulkController> logger)
            : base(antiforgery, logger)
        {
            _groups = groups;
        }

        /// <summary>
        /// Applies one operation to every group the request names, and reports how each one
        /// ended.
        /// </summary>
        /// <param name="request">Which operation, over which groups.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 with one entry per identifier, 400 when the body broke a rule on itself (an
        /// unknown operation, no identifiers, or more identifiers than one page holds), and
        /// 400 <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The same recorded departure the accounts controller makes for the same reason: the
        /// path carries a verb because a selection of rows is a set the table assembled in the
        /// browser, not a resource with an address of its own. The two refusals that protect
        /// the installation are unchanged by any of this and are not restated here - a base
        /// group answers <c>user_group_immutable</c> per entry and a group still holding
        /// members answers <c>user_group_in_use</c>, both from the single-group methods this
        /// calls.
        /// </para>
        /// <para>
        /// That is the answer to the question this endpoint is most likely to be asked: what
        /// happens to the three foundation rows sitting on the page. They are refused, each by
        /// name, and every other group on the page is still acted on. A run that refused the
        /// whole set because two of them were foundation rows would be a run that changed
        /// nothing for the most common selection this feature will ever be handed.
        /// </para>
        /// <para>
        /// No captcha and no attempt window, for the reason the rest of this controller gives.
        /// </para>
        /// </remarks>
        [HttpPost]
        [Authorize(Policy = AuthenticationExtensions.EditUserGroupsPolicy)]
        [ProducesResponseType<BulkOperationResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> BulkUserGroups(
            [FromBody] BulkUserGroupsRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var action = ParseBulkAction(request.Action);
            if (action is null)
            {
                errors[nameof(BulkUserGroupsRequest.Action)] = [BulkActionHint];
            }

            var ids = ParseIds(request.Ids, errors);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            return Ok(BulkResponses.Describe(await _groups.BulkAsync(
                new BulkUserGroupOperation
                {
                    Action = action!.Value,
                    GroupIds = ids!
                },
                cancellationToken)));
        }

        /// <summary>
        /// Turns the text a bulk body sent into the operation it names, or nothing at all.
        /// </summary>
        /// <remarks>
        /// Compared after lowering, so <c>Block</c> is the same request as <c>block</c>. Enum
        /// binding is deliberately not used, for the reason the accounts controller gives: it
        /// takes a member name spelled in full and would refuse the conventional one.
        /// </remarks>
        private static BulkUserGroupAction? ParseBulkAction(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "block" => BulkUserGroupAction.Block,
                "unblock" => BulkUserGroupAction.Unblock,
                "delete" => BulkUserGroupAction.Delete,
                _ => null
            };

        /// <summary>
        /// Turns the identifiers a bulk body sent into the rows they name, or nothing at all
        /// once one of them is wrong.
        /// </summary>
        /// <remarks>
        /// Duplicates collapse rather than being refused, for the reason the accounts
        /// controller gives: a page cannot hold the same row twice, so an overlap can only come
        /// from a client assembling the list by hand, and running an operation twice over one
        /// group is not what anybody means by selecting it.
        /// </remarks>
        private static IReadOnlyList<Guid>? ParseIds(
            IReadOnlyList<string>? ids,
            Dictionary<string, string[]> errors)
        {
            if (ids is null || ids.Count is 0)
            {
                errors[nameof(BulkUserGroupsRequest.Ids)] = ["Select at least one group."];
                return null;
            }

            if (ids.Count > BulkOperationLimits.MaxItems)
            {
                errors[nameof(BulkUserGroupsRequest.Ids)] =
                [$"At most {BulkOperationLimits.MaxItems} groups may be named in one request."];
                return null;
            }

            var parsed = new List<Guid>(ids.Count);

            foreach (var text in ids)
            {
                if (!Guid.TryParse(text, out var id))
                {
                    errors[nameof(BulkUserGroupsRequest.Ids)] = [IdsHint];
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
