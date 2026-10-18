using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Sessions;
using Fluxy.API.Extensions;
using Fluxy.API.Options;
using Fluxy.API.Security;
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
    /// Reading the installation's visit history, and ending a session of the accounts the
    /// caller may read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One policy on the class and a finer one on each action, exactly as
    /// <see cref="UsersController"/> does it. The class carries the role gate - every route here
    /// is an operator's route - and each action names the permission it needs, so an action
    /// added later is readable by every operator unless its author narrows it deliberately. The
    /// two are merged by the framework into one evaluation, so there is never one round trip of
    /// authorization on top of another.
    /// </para>
    /// <para>
    /// Both actions sit behind <c>ViewAnyUsersPolicy</c>, which demands <i>some</i> view
    /// permission and leaves which one to the service. That is the same coarse-and-fine pair the
    /// accounts list is built on: the policy knows the caller may read some of the
    /// installation's accounts, and only the request's own rows can say which. Ending a session
    /// is behind the view permission rather than the matching edit one, deliberately - this
    /// page is already scoped to exactly the accounts the caller may read, and a second gate on
    /// top of it would have to answer the same question twice to arrive at the same set.
    /// </para>
    /// <para>
    /// The route is <c>admin/sessions</c> and not a child of <c>admin/users</c>: what is listed
    /// here is sessions, not accounts, and the account the operator picks is a filter rather
    /// than a resource being addressed. It stays its own controller for the same reason the
    /// accounts controller is not also the groups controller - one controller, one resource.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/sessions")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class AdminSessionsController : AuthControllerBase
    {
        /// <summary>
        /// An empty grant set, returned when the account behind the request could not be read.
        /// Refuses everything, which is the safe reading of "we do not know who this is" - the
        /// policy on the action would already have refused such a request before it got here, so
        /// this is a floor rather than a branch anybody should be able to reach.
        /// </summary>
        private static readonly IReadOnlySet<UserPermission> NoGrants = new HashSet<UserPermission>();

        private readonly IAdminSessionHistoryService _sessions;

        /// <summary>
        /// Reader of this request's account, shared with the authorization handlers that ran on
        /// their way into the action.
        /// </summary>
        private readonly RequestStandingReader _standing;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminSessionsController"/> class.
        /// </summary>
        /// <param name="sessions">Service that reads the history and ends sessions.</param>
        /// <param name="standing">Reader of the account behind the request.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="throttle">Counter that limits how often one client may try.</param>
        /// <param name="rateLimits">Live limits, so a configuration reload applies without a restart.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public AdminSessionsController(
            IAdminSessionHistoryService sessions,
            RequestStandingReader standing,
            IAntiforgery antiforgery,
            IAttemptThrottle throttle,
            IOptionsMonitor<RateLimitOptions> rateLimits,
            ILogger<AdminSessionsController> logger)
            : base(antiforgery, throttle, rateLimits, logger)
        {
            _sessions = sessions;
            _standing = standing;
        }

        /// <summary>
        /// Reports one page of the installation's visit history, narrowed to the accounts this
        /// operator may read.
        /// </summary>
        /// <param name="page">One based page to read.</param>
        /// <param name="pageSize">How many visits one page holds.</param>
        /// <param name="search">
        /// Optional text that must appear in the address or the user agent of a visit.
        /// </param>
        /// <param name="userId">
        /// Optional account to restrict the list to, as a uuid. An account of a role this
        /// operator may not read simply matches no rows.
        /// </param>
        /// <param name="sortBy">What the page is ordered by; <c>visitedAt</c> by default.</param>
        /// <param name="sortOrder"><c>asc</c> or <c>desc</c>; <c>desc</c> by default.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 with the page and its total, 400 when a bound is out of range or a parameter
        /// names no value that exists, and 401/403 through the shared result handler.
        /// </returns>
        /// <remarks>
        /// Read only, and that decides what it is not guarded by. There is no antiforgery pair
        /// to present - the method changes nothing, so there is no state for a cross-site form
        /// submission to alter; no captcha, because the expensive step this API protects is key
        /// derivation and this path performs none; and no attempt window, because a page of rows
        /// from an index costs about as much as rendering the page that asked for it.
        /// <see cref="AuthControllerBase.NoStore"/> marks it, because the body names the
        /// networks every account the caller may read signed in from and a shared cache has no
        /// business handing that to the next visitor.
        ///
        /// The bounds are refused rather than clamped. Silently turning a request for 500 rows
        /// into 100 would answer with a page whose items do not match the page size the caller
        /// was told it got. The same rule covers the search term, the account filter and the two
        /// sort parameters: a nonsense <c>sortBy</c> is not quietly answered as
        /// <c>visitedAt</c>, because a caller that asked to order by something should be told it
        /// does not exist rather than handed a different order than the one it asked for.
        ///
        /// Search, filter and sort live here rather than in the browser for one reason: the
        /// pager shows a <c>total</c>, and a filter applied after the rows had arrived would
        /// count what the filter is about to remove - the result would be a pager offering four
        /// pages of five rows when only one exists.
        ///
        /// <c>sortBy</c> and <c>sortOrder</c> are read as plain text rather than bound to the
        /// enums, deliberately. Enum binding takes a member name spelled in full, so the
        /// conventional <c>sortOrder=desc</c> would be refused as a bad value while
        /// <c>sortOrder=Descending</c> would be accepted - an API that answers a request as
        /// conventional as this one with a 400 teaches its callers to spell things oddly.
        /// <c>userId</c> is text for the same reason: a uuid is what a filter is, and a value
        /// that is not one is a parameter worth refusing rather than a filter to ignore.
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewAnyUsersPolicy)]
        [ProducesResponseType<AdminSessionHistoryResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetSessions(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = SessionHistoryLimits.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] string? userId = null,
            [FromQuery] string sortBy = "visitedAt",
            [FromQuery] string sortOrder = "desc",
            CancellationToken cancellationToken = default)
        {
            NoStore();

            var field = ParseSortField(sortBy);
            var order = ParseSortOrder(sortOrder);

            // A filter that was not asked for and a filter that asked for nonsense are two
            // different answers, so the value and whether it was understood are read apart:
            // null here means "every account I may read", while a value that names no account
            // is a parameter worth refusing.
            Guid? wantedUser = null;
            var userOk = true;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                userOk = Guid.TryParse(userId, out var parsedUser);
                wantedUser = userOk ? parsedUser : null;
            }

            if (page < 1 ||
                pageSize is < 1 or > SessionHistoryLimits.MaxPageSize ||
                (search?.Length ?? 0) > SessionHistoryLimits.MaxSearchLength ||
                field is null ||
                order is null ||
                !userOk)
            {
                return StatusCode(
                    StatusCodes.Status400BadRequest,
                    new MessageResponse
                    {
                        Code = "validation_failed",
                        Message = "Some of the values you entered are not valid.",
                        Errors = ListErrors(page, pageSize, search, field, order, userOk)
                    });
            }

            var history = await _sessions.GetPageAsync(
                page,
                pageSize,
                search,
                wantedUser,
                field.Value,
                order.Value,
                await GrantedAsync(),
                ReadSessionClaim(),
                cancellationToken);

            return Ok(new AdminSessionHistoryResponse
            {
                Items = history.Visits
                    .Select(visit => new AdminSessionVisitResponse
                    {
                        Id = visit.Id.ToString(),
                        SessionId = visit.SessionId.ToString(),
                        UserId = visit.UserId.ToString(),
                        Username = visit.Username,
                        Ip = visit.Ip,
                        CountryCode = visit.CountryCode,
                        AutonomousSystemNumber = visit.AutonomousSystemNumber,
                        Organization = visit.Organization,
                        UserAgent = visit.UserAgent,
                        VisitedAt = visit.VisitedAt,
                        IsCurrent = visit.IsCurrent,
                        IsActive = visit.IsActive
                    })
                    .ToArray(),
                Total = history.Total,
                Page = history.Page,
                PageSize = history.PageSize
            });
        }

        /// <summary>
        /// Ends one session of an account this operator may read, so that no refresh token in
        /// its chain can be exchanged again.
        /// </summary>
        /// <param name="sessionId">Session to end, as listed by the history page.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 with the session ended, 400 for the session this request arrived with or a
        /// missing antiforgery token, 401 and 403 through the shared result handler, and 404
        /// when nothing was ended.
        /// </returns>
        /// <remarks>
        /// CSRF-guarded like every write of this API, and nothing else: no captcha (the
        /// expensive step this API protects on unauthenticated paths is key derivation, and a
        /// signed-in operator proving a session already cleared that), no attempt window (ending
        /// a session is a security act somebody reaches for when alarmed, and a window would make
        /// the second alarm of the day a refusal), no password (the operator's session is the
        /// proof of authority - the same rule sign-out follows).
        ///
        /// The check runs first, before any body is read and before any database work, so a
        /// request this application did not make is refused as such rather than as a 400 about a
        /// missing identifier - the same order every write of this API follows, and the reason
        /// the accounts controller states it in the same place.
        ///
        /// Every id that ends in 404 is one answer: unknown, already ended, quietly expired, or
        /// belonging to an account this operator may not read. The four are indistinguishable on
        /// purpose - the id is a guessable value, and a difference in the refusal would turn the
        /// endpoint into a probe for which accounts of the installation hold a signed-in session.
        ///
        /// The access token of an ended session cannot be recalled - it is signed and self
        /// contained - so it keeps working until it expires, bounded by
        /// <c>Auth:AccessLifetime</c>. Ending a session ends what keeps it alive: no refresh can
        /// be minted from the chain any more.
        /// </remarks>
        [HttpDelete("{sessionId:guid}")]
        [Authorize(Policy = AuthenticationExtensions.ViewAnyUsersPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RevokeSession(
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var outcome = await _sessions.RevokeAsync(
                sessionId,
                await GrantedAsync(),
                ReadSessionClaim(),
                cancellationToken);

            return outcome switch
            {
                AdminSessionRevokeOutcome.Revoked => Ok(new MessageResponse
                {
                    Code = ProfileController.SessionRevokedCode,
                    Message = "The session has been ended."
                }),

                AdminSessionRevokeOutcome.CannotRevokeCurrent => CannotRevokeCurrent(),

                _ => NotFound(new MessageResponse
                {
                    Code = ProfileController.SessionNotFoundCode,
                    Message = "No such session on an account you may read."
                })
            };
        }

        /// <summary>
        /// The permissions the account behind this request holds, for the service's per-target
        /// check.
        /// </summary>
        /// <remarks>
        /// Read here rather than in each action because it is the same question the policies on
        /// this controller already asked, answered once per request by
        /// <see cref="RequestStandingReader"/>: the coarse gate settled that the caller may read
        /// some accounts, and this is the same fact taken to the rows that say which. An account
        /// whose standing could not be established is granted nothing rather than everything -
        /// see <see cref="NoGrants"/>.
        /// </remarks>
        private async Task<IReadOnlySet<UserPermission>> GrantedAsync()
        {
            if (await _standing.ReadAsync(User) is { } found)
            {
                return found.Standing.Permissions;
            }

            return NoGrants;
        }

        /// <summary>
        /// Turns the text a client sent into the field it names, or nothing at all.
        /// </summary>
        /// <remarks>
        /// Compared after lowering, so <c>IP</c> and <c>VisitedAt</c> are the same request as
        /// their lowercase spelling - a parameter that is read case insensitively by every
        /// conventional API should not be the one exception here. The accepted values are also
        /// the only two the table can honestly order by: the moment and the address are stored,
        /// while the country and the provider are resolved from GeoIP while a page is built and
        /// are not in the row at all.
        /// </remarks>
        private static SessionSortField? ParseSortField(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "visitedat" => SessionSortField.VisitedAt,
                "ip" => SessionSortField.Ip,
                _ => null
            };

        /// <summary>Turns <c>asc</c> / <c>desc</c> into an order, or nothing at all.</summary>
        private static SessionSortOrder? ParseSortOrder(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "asc" => SessionSortOrder.Ascending,
                "desc" => SessionSortOrder.Descending,
                _ => null
            };

        /// <summary>
        /// Which of the bounds a request broke, keyed by the camelCase name of the query
        /// parameter so the reason lands under the input a client would fix.
        /// </summary>
        /// <remarks>
        /// A parameter that named no value gets the sentence listing the values that exist
        /// rather than a generic "not valid": the fastest way to fix a wrong <c>sortBy</c> is
        /// being told which two names are accepted.
        /// </remarks>
        private static Dictionary<string, string[]> ListErrors(
            int page,
            int pageSize,
            string? search,
            SessionSortField? field,
            SessionSortOrder? order,
            bool userIdOk)
        {
            var errors = new Dictionary<string, string[]>();

            if (page < 1)
            {
                errors["page"] = ["The page must be at least 1."];
            }

            if (pageSize is < 1 or > SessionHistoryLimits.MaxPageSize)
            {
                errors["pageSize"] =
                [$"The page size must be between 1 and {SessionHistoryLimits.MaxPageSize}."];
            }

            if ((search?.Length ?? 0) > SessionHistoryLimits.MaxSearchLength)
            {
                errors["search"] =
                [$"The search must be at most {SessionHistoryLimits.MaxSearchLength} characters."];
            }

            if (!userIdOk)
            {
                errors["userId"] = ["The user must be the identifier of an account, as a uuid."];
            }

            if (field is null)
            {
                errors["sortBy"] = ["Sort by must be 'visitedAt' or 'ip'."];
            }

            if (order is null)
            {
                errors["sortOrder"] = ["Sort order must be 'asc' or 'desc'."];
            }

            return errors;
        }

        /// <summary>
        /// The refusal for an attempt to end the session the request arrived with. The page
        /// disables the button for its own row; this is what a direct call that ignored the UI
        /// gets instead of a silent no-op, so "I pressed it and nothing happened" cannot be the
        /// state a caller is left in.
        /// </summary>
        private IActionResult CannotRevokeCurrent()
            => StatusCode(
                StatusCodes.Status400BadRequest,
                new MessageResponse
                {
                    Code = ProfileController.CannotRevokeCurrentCode,
                    Message = "The session you are using cannot be ended from here. Sign out instead."
                });

        private Guid? ReadSessionClaim()
            => Guid.TryParse(User.FindFirst(TokenClaimTypes.Session)?.Value, out var session)
                ? session
                : null;
    }
}
