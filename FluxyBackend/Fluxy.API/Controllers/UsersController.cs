using Fluxy.API.Configuration;
using Fluxy.API.Contracts;
using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Authentication;
using Fluxy.Core.Models.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Fluxy.API.Controllers
{
    /// <summary>
    /// Managing the accounts of the installation: one page of all of them, one in full, and
    /// the writes that make, change, confirm, block, unblock and delete them.
    /// </summary>
    /// <remarks>
    /// One policy on the class, not one per action: every route here is <c>AdminOnly</c>, so an
    /// action added later is an admin action unless its author changes the class. The policy
    /// runs before anything inside an action does, which turns a caller with no admin session
    /// into a 401 or 403 about the request rather than a 400 about a missing antiforgery token.
    ///
    /// <b>No captcha and no attempt window</b>, and both are absent for one reason: they exist
    /// to protect a request that has *no session* - the captcha guards the expensive step (key
    /// derivation) and the window caps how often an anonymous caller may reach it. Everything
    /// here is behind an admin session and an antiforgery pair, so the caller has already
    /// proved the two things those checks are there to prove. What is left is one bcrypt hash
    /// on a create or a password change, payable only by an operator who asked for it; a
    /// caller who wants to slow their own installation down has better tools than this.
    ///
    /// <b>The path carries an <c>admin</c> segment</b> where <c>/users</c> would have done,
    /// because this API keeps its audiences in the path the way its three sign-in doors do:
    /// <c>/admin/*</c> names the area being addressed, and it gives a gateway one prefix to
    /// refuse before the application is asked to decide anything. It is not an <c>api</c>
    /// segment - that distinction nobody needs is refused at the registration controller.
    ///
    /// <b>Three of the writes are RPC-shaped</b>: <c>POST {id}/confirm-registration</c>,
    /// <c>POST {id}/block</c> and <c>POST {id}/unblock</c>. This is the departure recorded in
    /// AGENTS.md, agreed before it was written. A state transition is not a field of the row
    /// the way <c>status</c> is - it is an operation with rules of its own: what it refuses,
    /// whose sessions it ends, whether it is idempotent. The alternative, patching
    /// <c>status</c> and letting the button decide what it meant, would make those rules a
    /// client-side convention, and the first client to skip them would put a blocked account's
    /// sessions back on the network. Note the difference the transport has to be able to
    /// express: blocking is idempotent (an account already blocked reports the same truth)
    /// while unblocking refuses an account that is not blocked (there is nowhere to go back
    /// to).
    ///
    /// <b>The three self-protections are refused by the service, not here.</b> An account may
    /// not delete itself, block itself or take its own level down. They are rules about what a
    /// request may ask for on behalf of its caller, which is not a fact this controller can
    /// keep to itself - the next endpoint reaching the same action would have to remember them,
    /// and it would not.
    ///
    /// Both reads carry <c>Cache-Control: no-store</c> through <see cref="AuthControllerBase.NoStore"/>:
    /// a shared cache that kept a page of accounts would hand one operator's list to the next.
    /// </remarks>
    [ApiController]
    [Route("admin/users")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class UsersController : AuthControllerBase
    {
        /// <summary>Sentence a filter that names no known level gets.</summary>
        private const string RoleHint =
            "Role must be 'client', 'reseller' or 'admin'.";

        /// <summary>Sentence a filter that names no known state gets.</summary>
        private const string StatusHint =
            "Status must be 'unregistered', 'registered' or 'blocked'.";

        /// <summary>Sentence a sort that names no column gets.</summary>
        private const string SortFieldHint =
            "Sort by must be 'username', 'email', 'role', 'status', 'lastSeenAt' or 'ip'.";

        private readonly IUserAdminService _users;

        /// <summary>
        /// Initializes a new instance of the <see cref="UsersController"/> class.
        /// </summary>
        /// <param name="users">Service that reads and changes the accounts.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="throttle">
        /// Counter shared with the rest of the authentication surface. It is not spent by any
        /// action of this controller - see the class remarks - but the base class takes it,
        /// because the window it counts is one the sign-in and refresh endpoints own.
        /// </param>
        /// <param name="rateLimits">Live limits, so a configuration reload applies without a restart.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public UsersController(
            IUserAdminService users,
            IAntiforgery antiforgery,
            IAttemptThrottle throttle,
            IOptionsMonitor<RateLimitOptions> rateLimits,
            ILogger<UsersController> logger)
            : base(antiforgery, throttle, rateLimits, logger)
        {
            _users = users;
        }

        /// <summary>
        /// Reports one page of every account: what the table draws, ordered and filtered as
        /// the query string asked.
        /// </summary>
        /// <param name="page">One based page to read; 1 by default.</param>
        /// <param name="pageSize">How many accounts one page holds; 20 by default.</param>
        /// <param name="search">
        /// Case-insensitive substring matched against the username or the email address.
        /// </param>
        /// <param name="role">Access level to keep, or nothing to keep every level.</param>
        /// <param name="status">State to keep, or nothing to keep every state.</param>
        /// <param name="sortBy">What the page is ordered by; <c>username</c> by default.</param>
        /// <param name="sortOrder"><c>asc</c> or <c>desc</c>; <c>asc</c> by default.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 with the page and its total, 400 when a bound is out of range or a parameter
        /// names no value that exists, and 401/403 through the shared result handler.
        /// </returns>
        /// <remarks>
        /// Search, filter and sort live here rather than in the browser for one reason: the
        /// pager shows a <c>total</c>. A filter applied after the rows arrived would count what
        /// it is about to remove, giving four pages of five rows when only one exists, and a
        /// sort applied there would order ten rows while the other pages kept whatever order
        /// the database happened to return - so the same account could appear twice or not at
        /// all.
        ///
        /// The bounds are refused rather than clamped. Silently turning a request for 500 rows
        /// into 100 would answer with a page whose items do not match the page size the caller
        /// was told it got. A <c>sortBy</c> that names nothing is refused for the same reason:
        /// a caller that asked to order by something should be told it does not exist rather
        /// than be handed a different order than the one it asked for.
        ///
        /// <c>role</c>, <c>status</c>, <c>sortBy</c> and <c>sortOrder</c> are read as plain
        /// text rather than bound to the enums, deliberately. Enum binding takes a member name
        /// spelled in full, so the conventional <c>sortOrder=desc</c> would be refused while
        /// <c>sortOrder=Descending</c> was accepted - an API that answers a request this
        /// conventional with a 400 teaches its callers to spell things oddly. The four are
        /// parsed below case insensitively, which is also where the sentence naming the values
        /// a client may send comes from; the framework's own message would say only that the
        /// value was wrong, not what would have been right.
        ///
        /// No antiforgery pair: the method changes nothing, so there is no state for a
        /// cross-site submission to alter. No captcha and no attempt window for the reason the
        /// class gives.
        /// </remarks>
        [HttpGet]
        [ProducesResponseType<AdminUserListResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = AdminUserListLimits.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] string? role = null,
            [FromQuery] string? status = null,
            [FromQuery] string? sortBy = "username",
            [FromQuery] string? sortOrder = "asc",
            CancellationToken cancellationToken = default)
        {
            NoStore();

            var field = ParseSortField(sortBy);
            var order = ParseSortOrder(sortOrder);

            // A filter that was not asked for and a filter that asked for nonsense are two
            // different answers, so the value and whether it was understood are read apart:
            // null here means "keep every level", while a value that names no level is a
            // parameter worth refusing.
            var wantedRole = ParseRoleOrNull(role);
            var wantedStatus = ParseStatusOrNull(status);
            var roleOk = wantedRole is not null || string.IsNullOrWhiteSpace(role);
            var statusOk = wantedStatus is not null || string.IsNullOrWhiteSpace(status);

            if (page < 1 ||
                pageSize is < 1 or > AdminUserListLimits.MaxPageSize ||
                (search?.Length ?? 0) > AdminUserListLimits.MaxSearchLength ||
                field is null ||
                order is null ||
                !roleOk ||
                !statusOk)
            {
                return Invalid(ListErrors(page, pageSize, search, field, order, roleOk, statusOk));
            }

            var list = await _users.GetPageAsync(
                page,
                pageSize,
                search,
                wantedRole,
                wantedStatus,
                field.Value,
                order.Value,
                cancellationToken);

            return Ok(new AdminUserListResponse
            {
                Items = list.Items.Select(ToItem).ToList(),
                Total = list.Total,
                Page = list.Page,
                PageSize = list.PageSize
            });
        }

        /// <summary>
        /// Reports one account in full, for the edit form.
        /// </summary>
        /// <param name="id">Identifier of the account.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with the account, or 404 when no account answers to that identifier.</returns>
        /// <remarks>
        /// A missing account and an identifier belonging to nobody are the same answer, and both
        /// are the same 404: the identifier is a uuid, so "not found" is the only thing that can
        /// be wrong with it and there is nothing to learn by splitting it further.
        ///
        /// The password and the pending confirmation code are absent rather than empty. The
        /// first cannot be shown back, the second is a credential, and a field that is present
        /// but blank invites a client to treat it as a value.
        /// </remarks>
        [HttpGet("{id:guid}")]
        [ProducesResponseType<AdminUserDetailResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUser(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            NoStore();

            var detail = await _users.GetAsync(id, cancellationToken);

            return detail is null
                ? NotFound(UserAdminResponses.Describe(
                    new AdminUserOutcome { Action = AdminUserAction.NotFound }).Body)
                : Ok(ToDetail(detail));
        }

        /// <summary>
        /// Creates an account from what the form sent.
        /// </summary>
        /// <param name="request">The account to create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 201 with a <c>Location</c> naming the new account, 400 with the rejected fields,
        /// 409 when another account holds that username or address, 400 <c>csrf_invalid</c>
        /// without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// Unlike the registration endpoint, this 201 carries a <c>Location</c>. There the row
        /// has no URI a client can address - it answers 401 to its own owner until the mailed
        /// code confirms it, and a pending-registration URI addressable by email would be the
        /// enumeration oracle the refusal policy exists to avoid. Here the account exists in
        /// full the moment it is written, this very controller reads it back, and a location
        /// the caller can act on immediately is exactly what the header is for. It is a
        /// relative reference (RFC 9110 allows one): the server does not know which host serves
        /// the frontend, and building an absolute address from a guess is how an open redirect
        /// gets introduced.
        ///
        /// The three required fields and the two enum names are checked here rather than by
        /// data annotations, for the reason the request type gives: an annotation is evaluated
        /// by the framework before the action runs, and a body that failed one would be answered
        /// without ever reaching the antiforgery check.
        /// </remarks>
        [HttpPost]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateUser(
            [FromBody] CreateUserRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(request.Username))
            {
                errors[nameof(CreateUserRequest.Username)] = ["A username is required."];
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                errors[nameof(CreateUserRequest.Email)] = ["An email address is required."];
            }

            if (string.IsNullOrEmpty(request.Password))
            {
                errors[nameof(CreateUserRequest.Password)] = ["A password is required."];
            }

            // An account with no level would be one every role check has to guess about, and
            // an account with no state would be neither registered nor waiting - both are
            // decided here rather than defaulted, because a default is a decision about
            // somebody else's account.
            if (!TryParseRole(request.Role, out var role))
            {
                errors[nameof(CreateUserRequest.Role)] = [RoleHint];
            }

            if (!TryParseStatus(request.Status, out var status))
            {
                errors[nameof(CreateUserRequest.Status)] = [StatusHint];
            }

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            var outcome = await _users.CreateAsync(
                new NewUser
                {
                    Username = request.Username ?? string.Empty,
                    Email = request.Email ?? string.Empty,
                    Password = request.Password ?? string.Empty,
                    Role = role,
                    Status = status,
                    TimeZone = request.TimeZone,
                    LoginGuard = ToSettings(request.LoginGuard)
                },
                cancellationToken);

            var (statusCode, body) = UserAdminResponses.Describe(outcome);

            if (statusCode == StatusCodes.Status201Created)
            {
                Response.Headers.Location = $"/admin/users/{outcome.UserId}";
            }

            return StatusCode(statusCode, body);
        }

        /// <summary>
        /// Applies the fields an edit body names to one account.
        /// </summary>
        /// <param name="id">Identifier of the account to change, in the path.</param>
        /// <param name="request">The fields to change. An absent field is left alone.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 when it was written, 400 with the rejected fields, 404 when there is no such
        /// account, 409 for a taken value or a state that makes the change meaningless, 409
        /// <c>cannot_demote_self</c> / <c>cannot_block_self</c> for a change that would take
        /// the caller's own access away.
        /// </returns>
        /// <remarks>
        /// The identifier travels in the path and never in the body. A form that posted its own
        /// account id as a field would be one edit away from writing it, and the path is also
        /// the honest statement of *which* account is meant - a body naming a different one
        /// would be answered as a conflict nobody could explain.
        ///
        /// Two side effects follow a change and are part of it rather than separate steps the
        /// client could forget: a status of Blocked ends every session the account holds, and a
        /// new password ends them too - whoever held the old password may hold a session with
        /// it, and the honest reading of that is to assume they do.
        /// </remarks>
        [HttpPatch("{id:guid}")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateUser(
            [FromRoute] Guid id,
            [FromBody] UpdateUserRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (ReadSubjectClaim() is not { } actingUserId)
            {
                return Anonymous();
            }

            // An absent property means "leave it alone", so only one that was named has to
            // name something that exists. A value that was named but names nothing - empty,
            // whitespace, or a level this system has never heard of - is refused rather than
            // treated as absent: the caller chose to send this field, and silently doing
            // nothing with it would hide the bug that made it empty.
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            UserRole? role = null;
            if (request.Role is { } roleText)
            {
                if (TryParseRole(roleText, out var parsedRole))
                {
                    role = parsedRole;
                }
                else
                {
                    errors[nameof(UpdateUserRequest.Role)] = [RoleHint];
                }
            }

            UserStatus? status = null;
            if (request.Status is { } statusText)
            {
                if (TryParseStatus(statusText, out var parsedStatus))
                {
                    status = parsedStatus;
                }
                else
                {
                    errors[nameof(UpdateUserRequest.Status)] = [StatusHint];
                }
            }

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            var outcome = await _users.UpdateAsync(
                id,
                new UserPatch
                {
                    Username = request.Username,
                    Email = request.Email,
                    Password = request.Password,
                    Role = role,
                    Status = status,
                    TimeZone = request.TimeZone,
                    LoginGuard = ToSettings(request.LoginGuard)
                },
                actingUserId,
                cancellationToken);

            return Answer(outcome);
        }

        /// <summary>
        /// Moves an account out of Unregistered, stamping the moment it happened.
        /// </summary>
        /// <param name="id">Identifier of the account.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>registration_confirmed</c>, 404 when there is no such account, 409
        /// <c>invalid_status</c> when the account is not waiting for a confirmation.
        /// </returns>
        /// <remarks>
        /// Refused rather than reported as a success when the account has already left
        /// Unregistered: the row the button was drawn for has changed under it since the page
        /// was read, and telling the caller otherwise would leave a stale list describing a
        /// state that is no longer there.
        ///
        /// The code this replaces is spent with it - the confirmation code was the way in for
        /// somebody who never used it, and a code mailed a week ago must not confirm an account
        /// somebody else already confirmed.
        /// </remarks>
        [HttpPost("{id:guid}/confirm-registration")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ConfirmRegistration(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await _users.ConfirmRegistrationAsync(id, cancellationToken));
        }

        /// <summary>
        /// Puts an account into Blocked.
        /// </summary>
        /// <param name="id">Identifier of the account.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>user_blocked</c>, 404 when there is no such account, 400
        /// <c>cannot_block_self</c> for the caller's own account.
        /// </returns>
        /// <remarks>
        /// Idempotent on purpose: the answer does not depend on where the block started, so a
        /// second call about an account a colleague blocked a moment ago reports the same truth
        /// rather than an error about a state nobody caused. It ends every session the account
        /// holds, because a blocked account is refused at sign-in and a session that outlives
        /// the block would keep working until its refresh token expired for no reason anybody
        /// could defend. The registration timestamp is left where it is - a block is not a
        /// cancellation of the registration.
        /// </remarks>
        [HttpPost("{id:guid}/block")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> BlockUser(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (ReadSubjectClaim() is not { } actingUserId)
            {
                return Anonymous();
            }

            return Answer(await _users.SetBlockedAsync(id, true, actingUserId, cancellationToken));
        }

        /// <summary>
        /// Takes an account out of Blocked, back to the state it was blocked from.
        /// </summary>
        /// <param name="id">Identifier of the account.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>user_unblocked</c>, 404 when there is no such account, 409
        /// <c>invalid_status</c> when the account is not blocked.
        /// </returns>
        /// <remarks>
        /// The inverse of the block and not of "set the status to Registered": an account that
        /// was never confirmed goes back to Unregistered, and no registration stamp is invented
        /// for it on the way out of a state that never gave it one.
        ///
        /// Refused rather than answered like the block above when the account is not blocked.
        /// Where an unblock *goes* depends on where the account came from, so an account that is
        /// not blocked has no answer to give - reporting success would mean claiming a state
        /// change that did not happen, and the caller is expected to re-read the list.
        /// </remarks>
        [HttpPost("{id:guid}/unblock")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UnblockUser(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (ReadSubjectClaim() is not { } actingUserId)
            {
                return Anonymous();
            }

            return Answer(await _users.SetBlockedAsync(id, false, actingUserId, cancellationToken));
        }

        /// <summary>
        /// Deletes an account and everything that references it.
        /// </summary>
        /// <param name="id">Identifier of the account to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>user_deleted</c>, 404 when there is no such account, 400
        /// <c>cannot_delete_self</c> for the caller's own account.
        /// </returns>
        /// <remarks>
        /// A <c>DELETE</c> that answers 200 rather than 204, because it has something to say:
        /// the caller has to know the difference between an account that was deleted and one
        /// that was not there to delete, and an empty body cannot say it.
        ///
        /// The refresh tokens, the pending changes and the guard rules go with the row through
        /// foreign keys that are <c>ON DELETE CASCADE</c> - a session or a code that outlives
        /// its account is a credential for nobody.
        /// </remarks>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteUser(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (ReadSubjectClaim() is not { } actingUserId)
            {
                return Anonymous();
            }

            return Answer(await _users.DeleteAsync(id, actingUserId, cancellationToken));
        }

        /// <summary>
        /// Turns the service's answer into the status and body a client is told, the one place
        /// where an <see cref="AdminUserAction"/> becomes a number.
        /// </summary>
        private static IActionResult Answer(AdminUserOutcome outcome)
        {
            var (status, body) = UserAdminResponses.Describe(outcome);

            return new ObjectResult(body) { StatusCode = status };
        }

        /// <summary>
        /// The 400 a body or a query string broke a rule on, with the fields that broke it.
        /// </summary>
        /// <param name="errors">
        /// Rejected fields, keyed by the C# name of the property or by the camelCase name of a
        /// query parameter - both arrive in one shape after the rewrite.
        /// </param>
        private static ObjectResult Invalid(Dictionary<string, string[]> errors)
            => new(new MessageResponse
            {
                Code = "validation_failed",
                Message = "Some of the values you entered are not valid.",
                Errors = FieldErrorKeys.FromPropertyNames(errors)
            })
            { StatusCode = StatusCodes.Status400BadRequest };

        /// <summary>
        /// Turns the text a client sent into the column it names, or nothing at all.
        /// </summary>
        /// <remarks>
        /// Compared after lowering, so <c>IP</c> and <c>Username</c> are the same request as
        /// their lowercase spelling - a parameter read case insensitively by every conventional
        /// API should not be the one exception here. The accepted values are the only columns
        /// the table can honestly order by: everything else it might want to show (the country
        /// behind an address, for one) is resolved while the page is built and is not in the
        /// row at all.
        /// </remarks>
        private static AdminUserSortField? ParseSortField(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "username" => AdminUserSortField.Username,
                "email" => AdminUserSortField.Email,
                "role" => AdminUserSortField.Role,
                "status" => AdminUserSortField.Status,
                "lastseenat" => AdminUserSortField.LastSeenAt,
                "ip" => AdminUserSortField.Ip,
                _ => null
            };

        /// <summary>Turns <c>asc</c> / <c>desc</c> into an order, or nothing at all.</summary>
        private static AdminUserSortOrder? ParseSortOrder(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "asc" => AdminUserSortOrder.Ascending,
                "desc" => AdminUserSortOrder.Descending,
                _ => null
            };

        /// <summary>
        /// Turns the text of a filter into the level it names, or null when it names none.
        /// </summary>
        /// <remarks>
        /// Compared after lowering, so <c>Admin</c> and <c>admin</c> are the same request - a
        /// value read case insensitively by every conventional API should not be the one
        /// exception. Absent and blank come back null as well, which means "no level"; whether
        /// that is a filter nobody asked for or a field nobody filled in is for the caller to
        /// say, and it is the caller that decides which of the two it accepts.
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
        /// Turns the text a body named into the level it is.
        /// </summary>
        /// <param name="value">The value the body carried, or null when it carried none.</param>
        /// <param name="role">
        /// The level, or <see cref="UserRole.Client"/> when the answer is false - assigned
        /// because an <c>out</c> parameter has to be assigned, and never read, because every
        /// caller returns on a false.
        /// </param>
        /// <returns>
        /// False when the value is absent or names no level that exists. Absent is a failure
        /// here rather than "no filter": the body named the field, and a body that named it
        /// without saying what it is has a bug worth reporting rather than a default worth
        /// guessing - a role left at a default would be a decision about somebody else's
        /// account.
        /// </returns>
        private static bool TryParseRole(string? value, out UserRole role)
        {
            if (ParseRoleOrNull(value) is { } parsed)
            {
                role = parsed;

                return true;
            }

            role = default;

            return false;
        }

        /// <summary>
        /// Turns the text of a filter into the state it names, or null when it names none.
        /// </summary>
        /// <remarks>
        /// Matched member by member rather than with an ordered value, because
        /// <see cref="UserStatus"/> is not ordered: Unregistered, Registered and Blocked are
        /// three states of one account, not three levels of anything.
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
        /// Turns the text a body named into the state it is.
        /// </summary>
        /// <param name="value">The value the body carried, or null when it carried none.</param>
        /// <param name="status">
        /// The state, or <see cref="UserStatus.Unregistered"/> when the answer is false - see
        /// the note beside <see cref="TryParseRole"/>.
        /// </param>
        /// <returns>False when the value is absent or names no state that exists.</returns>
        private static bool TryParseStatus(string? value, out UserStatus status)
        {
            if (ParseStatusOrNull(value) is { } parsed)
            {
                status = parsed;

                return true;
            }

            status = default;

            return false;
        }

        /// <summary>
        /// Which of the bounds a request broke, keyed by the camelCase name of the query
        /// parameter so the reason lands under the input a client would fix.
        /// </summary>
        /// <remarks>
        /// A parameter that names no value gets the sentence listing the values that exist,
        /// which is the same sentence <c>TryParseRole</c>, <c>TryParseStatus</c> and
        /// <c>ParseSortField</c> would have given - collected here so that one request with
        /// three mistakes is told about all three rather than one per round trip.
        /// </remarks>
        private static Dictionary<string, string[]> ListErrors(
            int page,
            int pageSize,
            string? search,
            AdminUserSortField? field,
            AdminUserSortOrder? order,
            bool roleOk,
            bool statusOk)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (page < 1)
            {
                errors["page"] = ["The page must be at least 1."];
            }

            if (pageSize is < 1 or > AdminUserListLimits.MaxPageSize)
            {
                errors["pageSize"] =
                [
                    $"The page size must be between 1 and {AdminUserListLimits.MaxPageSize}."
                ];
            }

            if ((search?.Length ?? 0) > AdminUserListLimits.MaxSearchLength)
            {
                errors["search"] =
                [
                    $"The search must be at most {AdminUserListLimits.MaxSearchLength} characters."
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

        /// <summary>The guard as a request carries it, or null when the body named none.</summary>
        private static LoginGuardSettings? ToSettings(PatchLoginGuardRequest? request)
            => request is null
                ? null
                : new LoginGuardSettings
                {
                    GeoProtectionEnabled = request.GeoProtectionEnabled,
                    BindSessionToIp = request.BindSessionToIp,
                    AllowedIps = request.AllowedIps ?? [],
                    AllowedCountry = request.AllowedCountry,
                    AllowedAutonomousSystemNumber = request.AllowedAutonomousSystemNumber
                };

        private static AdminUserItemResponse ToItem(AdminUser item) => new()
        {
            Id = item.Id.ToString(),
            Username = item.Username,
            Email = item.Email,
            Role = item.Role.ToString(),
            Status = item.Status.ToString(),
            LastSeenAt = item.LastSeenAt,
            LastIp = item.LastIp
        };

        private static AdminUserDetailResponse ToDetail(AdminUserDetail detail) => new()
        {
            Id = detail.Id.ToString(),
            Username = detail.Username,
            Email = detail.Email,
            Role = detail.Role.ToString(),
            Status = detail.Status.ToString(),
            TimeZone = detail.TimeZone,
            RegisteredAt = detail.RegisteredAt,
            CreatedAt = detail.CreatedAt,
            LastSeenAt = detail.LastSeenAt,
            LastIp = detail.LastIp,
            LoginGuard = new LoginGuardResponse
            {
                GeoProtectionEnabled = detail.LoginGuard.GeoProtectionEnabled,
                BindSessionToIp = detail.LoginGuard.BindSessionToIp,
                AllowedIps = detail.LoginGuard.AllowedIps,
                AllowedCountry = detail.LoginGuard.AllowedCountry,
                AllowedAutonomousSystemNumber = detail.LoginGuard.AllowedAutonomousSystemNumber
            }
        };

        /// <summary>
        /// The refusal an authenticated request gets when it carries no subject to act for.
        /// Already signed in by the policy on the class, so a token without a subject is not a
        /// visitor who forgot to sign in - it is a token this application would not have minted.
        /// </summary>
        private IActionResult Anonymous()
            => StatusCode(
                StatusCodes.Status401Unauthorized,
                new MessageResponse
                {
                    Code = "auth_required",
                    Message = "Your session has ended. Please sign in again."
                });

        private Guid? ReadSubjectClaim()
            => Guid.TryParse(User.FindFirst(TokenClaimTypes.Subject)?.Value, out var userId)
                ? userId
                : null;
    }
}
