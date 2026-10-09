using Fluxy.API.Configuration;
using Fluxy.API.Contracts;
using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Fluxy.API.Controllers
{
    /// <summary>
    /// Managing the groups of the installation: one page of all of them, one in full, and the
    /// writes that create, change and delete them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One policy on the class, not one per action: every route here is <c>AdminOnly</c>, so an
    /// action added later is an admin action unless its author changes the class. Each action
    /// then names the permission it needs on top of that - reads demand <c>viewUserGroups</c>,
    /// writes demand <c>editUserGroups</c> - and the framework merges the two into one
    /// evaluation. The role is the door and the permission is the room, which is the whole
    /// shape of the mechanism in one sentence.
    /// </para>
    /// <para>
    /// <b>No captcha and no attempt window</b>, for the reason the accounts controller gives:
    /// both exist to guard a request with no session at all, and everything here is behind an
    /// admin session and a fresh antiforgery pair.
    /// </para>
    /// <para>
    /// <b>The route is plural and carries the <c>admin</c> segment</b> beside the accounts
    /// resource rather than nested under it. A group is not a page of an account - it is a row
    /// of its own with a name, a state and a set of keys, and it is chosen by accounts that may
    /// not be on the page being edited. Nesting it under a particular account would name the
    /// relationship backwards.
    /// </para>
    /// <para>
    /// <b>Base groups are refused by the service, not hidden by this controller.</b> The
    /// <c>isBase</c> flag on the responses is what a form draws its disabled controls from, and
    /// the 409 <c>user_group_immutable</c> is what an endpoint that forgot to draw them gets.
    /// The two are deliberately different layers: a control that forgot to disable itself is a
    /// cosmetic bug, while a write that reached the service without being refused would be an
    /// installation whose three foundation rows are reshapeable by anyone who can find the URL.
    /// </para>
    /// <para>
    /// Both reads carry <c>Cache-Control: no-store</c> through
    /// <see cref="AuthControllerBase.NoStore"/>: this is a page of who may do what, and a
    /// shared cache that kept one would hand it to the next operator.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/user-groups")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class UserGroupsController : AuthControllerBase
    {
        /// <summary>Sentence a role filter that names no known member gets.</summary>
        private const string RoleHint =
            "The role must be 'client', 'reseller' or 'admin'.";

        /// <summary>Sentence a status filter that names no known member gets.</summary>
        private const string StatusHint =
            "Status must be 'unregistered', 'registered' or 'blocked'.";

        /// <summary>Sentence a sort that names no column gets.</summary>
        private const string SortFieldHint =
            "Sort by must be 'name', 'role', 'status', 'permissionsCount' or 'createdAt'.";

        /// <summary>Sentence the list of permissions gets when one of them names nothing.</summary>
        private const string PermissionHint =
            "Unknown permission. The permissions that exist are: "
                + "viewUsers, editUsers, viewUserGroups, editUserGroups.";

        private readonly IUserGroupService _groups;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserGroupsController"/> class.
        /// </summary>
        /// <param name="groups">Service that reads and changes the groups.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="throttle">
        /// Counter shared with the rest of the authentication surface. It is not spent by any
        /// action of this controller - the window it counts belongs to sign-in and refresh -
        /// but the base class takes it, so the two controllers share one answer to "which
        /// window?".
        /// </param>
        /// <param name="rateLimits">Live limits, so a configuration reload applies without a restart.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public UserGroupsController(
            IUserGroupService groups,
            IAntiforgery antiforgery,
            IAttemptThrottle throttle,
            IOptionsMonitor<RateLimitOptions> rateLimits,
            ILogger<UserGroupsController> logger)
            : base(antiforgery, throttle, rateLimits, logger)
        {
            _groups = groups;
        }

        /// <summary>
        /// Reports one page of every group: what the table draws, ordered and filtered as the
        /// query string asked.
        /// </summary>
        /// <param name="page">One based page to read; 1 by default.</param>
        /// <param name="pageSize">How many groups one page holds; 20 by default.</param>
        /// <param name="search">Case-insensitive substring matched against the name.</param>
        /// <param name="role">Level to keep, or nothing to keep every level.</param>
        /// <param name="status">State to keep, or nothing to keep every state.</param>
        /// <param name="sortBy">What the page is ordered by; <c>name</c> by default.</param>
        /// <param name="sortOrder"><c>asc</c> or <c>desc</c>; <c>asc</c> by default.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 with the page and its total, or 400 when a bound is out of range or a parameter
        /// names no value that exists.
        /// </returns>
        /// <remarks>
        /// Search, filter and order are the server's for the reason the accounts list gives:
        /// the pager shows a <c>total</c>, and one applied in the browser to the rows already
        /// on screen would count what it is about to remove.
        ///
        /// The same bounds are refused rather than clamped, and the same parsing is case
        /// insensitive so <c>sortOrder=desc</c> and <c>sortBy=Name</c> both work. A
        /// <c>role</c> filter that names no member is refused rather than ignored: silently
        /// dropping a filter answers with every group, which reads exactly like a filter that
        /// matched.
        ///
        /// A picker that wants all of them asks for one page of
        /// <see cref="UserGroupListLimits.MaxPageSize"/>, rather than this resource growing a
        /// second unpaged route - the count a picker would ignore is the same number the table
        /// needs, and one route is one thing to keep correct.
        ///
        /// No antiforgery pair: the method changes nothing.
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewUserGroupsPolicy)]
        [ProducesResponseType<AdminUserGroupListResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetGroups(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = UserGroupListLimits.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] string? role = null,
            [FromQuery] string? status = null,
            [FromQuery] string? sortBy = "name",
            [FromQuery] string? sortOrder = "asc",
            CancellationToken cancellationToken = default)
        {
            NoStore();

            var field = ParseSortField(sortBy);
            var order = ParseSortOrder(sortOrder);

            var wantedRole = ParseRoleOrNull(role);
            var wantedStatus = ParseStatusOrNull(status);

            // Read apart from whether the value was understood: null means "keep everything",
            // while a value that names nothing is a parameter worth refusing.
            var roleOk = wantedRole is not null || string.IsNullOrWhiteSpace(role);
            var statusOk = wantedStatus is not null || string.IsNullOrWhiteSpace(status);

            if (page < 1 ||
                pageSize is < 1 or > UserGroupListLimits.MaxPageSize ||
                (search?.Length ?? 0) > UserGroupListLimits.MaxSearchLength ||
                field is null ||
                order is null ||
                !roleOk ||
                !statusOk)
            {
                return Invalid(ListErrors(page, pageSize, search, field, order, roleOk, statusOk));
            }

            var list = await _groups.GetPageAsync(
                page,
                pageSize,
                search,
                wantedRole,
                wantedStatus,
                field.Value,
                order.Value,
                cancellationToken);

            return Ok(new AdminUserGroupListResponse
            {
                Items = list.Items.Select(ToItem).ToList(),
                Total = list.Total,
                Page = list.Page,
                PageSize = list.PageSize
            });
        }

        /// <summary>
        /// Reports one group in full, for the edit form.
        /// </summary>
        /// <param name="id">Identifier of the group.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with the group, or 404 when no group answers to that identifier.</returns>
        /// <remarks>
        /// A missing group and an identifier belonging to nobody are the same 404, because a
        /// uuid has only one way to be wrong.
        ///
        /// The permissions come back as individual keys rather than as a bitmask number. A
        /// client that had to decode <c>15</c> would be one that has to know which bit the
        /// system set next, which is the thing a permission catalog exists to keep stable.
        /// </remarks>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.ViewUserGroupsPolicy)]
        [ProducesResponseType<AdminUserGroupDetailResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetGroup(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            NoStore();

            var detail = await _groups.GetAsync(id, cancellationToken);

            return detail is null
                ? NotFound(UserGroupResponses.Describe(
                    new UserGroupOutcome { Action = UserGroupAction.NotFound }).Body)
                : Ok(ToDetail(detail));
        }

        /// <summary>
        /// Creates a group from what the form sent.
        /// </summary>
        /// <param name="request">The group to create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 201 with a <c>Location</c> naming the new group, 400 with the rejected fields, 409
        /// when another group holds that name, 400 <c>csrf_invalid</c> without the antiforgery
        /// pair.
        /// </returns>
        /// <remarks>
        /// The 201 carries a <c>Location</c>, and unlike the registration endpoint the header
        /// is honest: the row exists in full the moment it is written, this very controller
        /// reads it back, and nothing stands between creation and addressability.
        ///
        /// A permission that names nothing is refused with the list that does exist, rather
        /// than dropped. The two refusals are different on purpose: a permission the role may
        /// not hold is dropped because it would mean nothing, while a permission that does not
        /// exist at all is a typo the caller has no way to notice, and a form that reported
        /// success for a key nobody will ever check would be a form that hid its own bug.
        /// </remarks>
        [HttpPost]
        [Authorize(Policy = AuthenticationExtensions.EditUserGroupsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateGroup(
            [FromBody] CreateUserGroupRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            // A group with no level would be one whose members hold nothing anybody could
            // answer for, and a group with no state would be neither registered nor blocked.
            // Both are decided here rather than defaulted, because a default is a decision
            // about somebody else's group.
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                errors[nameof(CreateUserGroupRequest.Name)] = ["A name is required."];
            }

            UserRole? role = null;
            if (request.Role is { } roleText)
            {
                if (ParseRoleOrNull(roleText) is { } parsedRole)
                {
                    role = parsedRole;
                }
                else
                {
                    errors[nameof(CreateUserGroupRequest.Role)] = [RoleHint];
                }
            }
            else
            {
                errors[nameof(CreateUserGroupRequest.Role)] = [RoleHint];
            }

            UserStatus? status = null;
            if (request.Status is { } statusText)
            {
                if (ParseStatusOrNull(statusText) is { } parsedStatus)
                {
                    status = parsedStatus;
                }
                else
                {
                    errors[nameof(CreateUserGroupRequest.Status)] = [StatusHint];
                }
            }
            else
            {
                errors[nameof(CreateUserGroupRequest.Status)] = [StatusHint];
            }

            var permissions = ParsePermissions(request.Permissions, errors);

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            var outcome = await _groups.CreateAsync(
                new NewUserGroup
                {
                    Name = request.Name?.Trim() ?? string.Empty,
                    Role = role!.Value,
                    Status = status!.Value,
                    Permissions = permissions
                },
                cancellationToken);

            var (statusCode, body) = UserGroupResponses.Describe(outcome);

            if (statusCode == StatusCodes.Status201Created)
            {
                Response.Headers.Location = $"/admin/user-groups/{outcome.GroupId}";
            }

            return StatusCode(statusCode, body);
        }

        /// <summary>
        /// Applies the fields an edit body names to one group.
        /// </summary>
        /// <param name="id">Identifier of the group to change, in the path.</param>
        /// <param name="request">The fields to change. An absent field is left alone.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 when it was written, 400 with the rejected fields, 404 when there is no such
        /// group, 409 for a taken name, <c>user_group_immutable</c> for a base group that was
        /// asked to change more than its name.
        /// </returns>
        /// <remarks>
        /// The identifier travels in the path and never in the body, for the reason the accounts
        /// endpoint gives: a form that posted its own id would be one edit away from writing it,
        /// and the path is the honest statement of which group is meant.
        ///
        /// A body that changes nothing is a success rather than a complaint. Sending a role the
        /// group already has is a fact about the row, not a mistake, and refusing it would make
        /// a client unable to save a form without first computing which fields actually moved.
        /// </remarks>
        [HttpPatch("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditUserGroupsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateGroup(
            [FromRoute] Guid id,
            [FromBody] UpdateUserGroupRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            // An absent property means "leave it alone", so only one that was named has to
            // name something that exists.
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            UserRole? role = null;
            if (request.Role is { } roleText)
            {
                if (ParseRoleOrNull(roleText) is { } parsedRole)
                {
                    role = parsedRole;
                }
                else
                {
                    errors[nameof(UpdateUserGroupRequest.Role)] = [RoleHint];
                }
            }

            UserStatus? status = null;
            if (request.Status is { } statusText)
            {
                if (ParseStatusOrNull(statusText) is { } parsedStatus)
                {
                    status = parsedStatus;
                }
                else
                {
                    errors[nameof(UpdateUserGroupRequest.Status)] = [StatusHint];
                }
            }

            // Null here means "leave the set alone", which is why the parse runs apart from
            // the null check: an empty list clears every grant and a missing one changes none,
            // and that difference is the only way a body can say either.
            var permissions = request.Permissions is null
                ? null
                : ParsePermissions(request.Permissions, errors);

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            var outcome = await _groups.UpdateAsync(
                id,
                new UserGroupPatch
                {
                    Name = request.Name,
                    Role = role,
                    Status = status,
                    Permissions = permissions
                },
                cancellationToken);

            return Answer(outcome);
        }

        /// <summary>
        /// Deletes a group and its permissions.
        /// </summary>
        /// <param name="id">Identifier of the group to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>user_group_deleted</c>, 404 when there is no such group, 409
        /// <c>user_group_in_use</c> when accounts still belong to it, 409
        /// <c>user_group_immutable</c> for one of the three base groups.
        /// </returns>
        /// <remarks>
        /// A <c>DELETE</c> that answers 200 rather than 204, because it has something to say:
        /// the difference between a group that was deleted and one that was not there to delete
        /// is the whole answer, and an empty body cannot say it.
        ///
        /// The refusal comes before the write rather than from the database's foreign key. Both
        /// would refuse, and only one of them can explain itself: an <c>ON DELETE RESTRICT</c>
        /// violation arrives as a provider error naming a constraint nobody chose, while this
        /// arrives as a sentence about accounts that still belong here.
        /// </remarks>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditUserGroupsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteGroup(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await _groups.DeleteAsync(id, cancellationToken));
        }

        /// <summary>
        /// Turns the service's answer into the status and body a client is told, the one place
        /// where a <see cref="UserGroupAction"/> becomes a number.
        /// </summary>
        private static IActionResult Answer(UserGroupOutcome outcome)
        {
            var (status, body) = UserGroupResponses.Describe(outcome);

            return new ObjectResult(body) { StatusCode = status };
        }

        /// <summary>
        /// The 400 a body or a query string broke a rule on, with the fields that broke it.
        /// </summary>
        private static ObjectResult Invalid(Dictionary<string, string[]> errors)
            => new(new MessageResponse
            {
                Code = "validation_failed",
                Message = "Some of the values you entered are not valid.",
                Errors = FieldErrorKeys.FromPropertyNames(errors)
            })
            { StatusCode = StatusCodes.Status400BadRequest };

        /// <summary>
        /// Reads every name in a permission list, refusing the ones that name nothing.
        /// </summary>
        /// <remarks>
        /// The parse and the refusal live together so that a half-correct list is refused whole
        /// rather than quietly shortened: a client that sent two permissions and was given one
        /// back would have no way to learn which of them it misspelled.
        /// </remarks>
        /// <param name="values">Names as sent, or null when the body carried none.</param>
        /// <param name="errors">Where a name that names nothing is reported.</param>
        /// <returns>The permissions that were understood, possibly empty.</returns>
        private static IReadOnlySet<UserPermission> ParsePermissions(
            IReadOnlyList<string>? values,
            Dictionary<string, string[]> errors)
        {
            var result = new HashSet<UserPermission>();

            if (values is null)
            {
                return result;
            }

            foreach (var value in values)
            {
                if (UserPermissionCatalog.TryParse(value, out var permission))
                {
                    result.Add(permission);
                }
                else
                {
                    // Only added once: a body that misspelled the same key three times should
                    // be told about one mistake, not three.
                    errors.TryAdd(nameof(CreateUserGroupRequest.Permissions), [PermissionHint]);
                }
            }

            return result;
        }

        /// <summary>Turns <c>asc</c> / <c>desc</c> into an order, or nothing at all.</summary>
        private static UserGroupSortOrder? ParseSortOrder(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "asc" => UserGroupSortOrder.Ascending,
                "desc" => UserGroupSortOrder.Descending,
                _ => null
            };

        /// <summary>
        /// Turns the text a client sent into the column it names, or nothing at all.
        /// </summary>
        /// <remarks>
        /// Compared after lowering so <c>Name</c> and <c>PERMISSIONSCOUNT</c> are the same
        /// request as their lowercase spelling. The identifier is deliberately absent: a random
        /// uuid is an order nobody asked for, exactly as on the accounts list.
        /// </remarks>
        private static UserGroupSortField? ParseSortField(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "name" => UserGroupSortField.Name,
                "role" => UserGroupSortField.Role,
                "status" => UserGroupSortField.Status,
                "permissionscount" => UserGroupSortField.PermissionsCount,
                "createdat" => UserGroupSortField.CreatedAt,
                _ => null
            };

        /// <summary>
        /// Turns the text of a filter into the level it names, or null when it names none.
        /// </summary>
        /// <remarks>
        /// Matched member by member rather than with an ordered value, because the same
        /// sentence the accounts list gives applies: a level is a member of an enum, and a
        /// number outside them would be a level every check has to guess about.
        /// </remarks>
        private static UserRole? ParseRoleOrNull(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "client" => UserRole.Client,
                "reseller" => UserRole.Reseller,
                "admin" => UserRole.Admin,
                _ => null
            };

        /// <summary>
        /// Turns the text of a filter into the state it names, or null when it names none.
        /// </summary>
        /// <remarks>
        /// Matched member by member because <see cref="UserStatus"/> is not ordered: three
        /// states of one row, not three levels of anything.
        /// </remarks>
        private static UserStatus? ParseStatusOrNull(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "unregistered" => UserStatus.Unregistered,
                "registered" => UserStatus.Registered,
                "blocked" => UserStatus.Blocked,
                _ => null
            };

        /// <summary>
        /// Which of the bounds a request broke, keyed by the camelCase name of the parameter so
        /// the reason lands under the input a client would fix.
        /// </summary>
        private static Dictionary<string, string[]> ListErrors(
            int page,
            int pageSize,
            string? search,
            UserGroupSortField? field,
            UserGroupSortOrder? order,
            bool roleOk,
            bool statusOk)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (page < 1)
            {
                errors["page"] = ["The page must be at least 1."];
            }

            if (pageSize is < 1 or > UserGroupListLimits.MaxPageSize)
            {
                errors["pageSize"] =
                [
                    $"The page size must be between 1 and {UserGroupListLimits.MaxPageSize}."
                ];
            }

            if ((search?.Length ?? 0) > UserGroupListLimits.MaxSearchLength)
            {
                errors["search"] =
                [
                    $"The search must be at most {UserGroupListLimits.MaxSearchLength} characters."
                ];
            }

            if (!roleOk)
            {
                errors["role"] = [RoleHint];
            }

            if (!statusOk)
            {
                errors["status"] = [StatusHint];
            }

            if (field is null)
            {
                errors["sortBy"] = [SortFieldHint];
            }

            if (order is null)
            {
                errors["sortOrder"] = ["Sort order must be 'asc' or 'desc'."];
            }

            return errors;
        }

        private static AdminUserGroupItemResponse ToItem(UserGroupListItem item) => new()
        {
            Id = item.Id.ToString(),
            Name = item.Name,
            Role = item.Role.ToString(),
            Status = item.Status.ToString(),
            PermissionsCount = item.PermissionsCount,
            IsBase = item.IsBase
        };

        /// <summary>
        /// The permissions arrive in the catalog's own order rather than in whatever order the
        /// rows happened to be written in, so a form draws the same list on every group.
        /// </summary>
        private static AdminUserGroupDetailResponse ToDetail(UserGroupDetail detail) => new()
        {
            Id = detail.Id.ToString(),
            Name = detail.Name,
            Role = detail.Role.ToString(),
            Status = detail.Status.ToString(),
            Permissions = UserPermissionCatalog.All
                .Where(detail.Permissions.Contains)
                .Select(UserPermissionCatalog.NameOf)
                .ToList(),
            IsBase = detail.IsBase,
            Members = detail.Members,
            CreatedAt = detail.CreatedAt,
            UpdatedAt = detail.UpdatedAt
        };
    }
}
