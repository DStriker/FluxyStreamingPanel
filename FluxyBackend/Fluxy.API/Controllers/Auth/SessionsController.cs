using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Sessions;
using Fluxy.API.Controllers.Support;
using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Auth
{
    /// <summary>
    /// The visit history and the live sessions of the signed-in account: where its sessions
    /// came from, which are still open, and the two ways to end them.
    /// </summary>
    /// <remarks>
    /// Split from <c>ProfileController</c>, and the seam is a resource rather than a line
    /// count: a profile is what the account is, a session is something it holds for a while.
    /// The two share the <c>auth</c> route, the session the whole class demands and the
    /// storage behind them; the account identifier still comes only from the token, so there
    /// is no <c>userId</c> to pass and a caller can only ever read and end its own.
    ///
    /// No captcha and no attempt window, and that is a decision rather than an omission: the
    /// history is a read over an index, and ending sessions is what an alarmed person reaches
    /// for. Every revoke carries the antiforgery pair; the reads carry nothing but the
    /// session.
    ///
    /// Both reads carry <c>Cache-Control: no-store</c>, because a shared cache that kept one
    /// account's sessions would hand them to the next visitor.
    /// </remarks>
    [ApiController]
    [Route("auth")]
    [Authorize]
    [Produces("application/json")]
    public sealed class SessionsController : ApiControllerBase
    {
        /// <summary>
        /// Sentence a blocked or missing account is refused with on every read and every act
        /// of this controller. The code is the one the profile change flow already answers
        /// with, so a client branching on <c>account_not_active</c> behaves the same way for
        /// an account that lost its standing here as it does on the profile - while the
        /// sentence stays one about the account rather than about an edit nobody attempted.
        /// </summary>
        private const string AccountNotActiveMessage = "This account is not active.";

        private readonly IProfileService _profileService;
        private readonly ITokenService _tokenService;
        private readonly ISessionHistoryService _sessionHistory;
        private readonly IActiveSessionService _activeSessions;

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionsController"/> class.
        /// </summary>
        /// <param name="profileService">Service that answers whether the account still stands.</param>
        /// <param name="tokenService">Service that ends the sessions a revoke names.</param>
        /// <param name="sessionHistory">Reader of the account's stored sessions.</param>
        /// <param name="activeSessions">Reader of the account's live sessions.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public SessionsController(
            IProfileService profileService,
            ITokenService tokenService,
            ISessionHistoryService sessionHistory,
            IActiveSessionService activeSessions,
            IAntiforgery antiforgery,
            ILogger<SessionsController> logger)
            : base(antiforgery, logger)
        {
            _profileService = profileService;
            _tokenService = tokenService;
            _sessionHistory = sessionHistory;
            _activeSessions = activeSessions;
        }

        /// <summary>
        /// Reports one page of the account's visit history: where its sessions came from, what
        /// they arrived with, and when.
        /// </summary>
        /// <param name="page">One based page to read.</param>
        /// <param name="pageSize">How many visits one page holds.</param>
        /// <param name="search">
        /// Optional text that must appear in the address or the user agent of a visit.
        /// </param>
        /// <param name="sortBy">What the page is ordered by; <c>visitedAt</c> by default.</param>
        /// <param name="sortOrder">
        /// <c>asc</c> or <c>desc</c>; <c>desc</c> by default.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 with the page, 400 when a bound was not a number, is out of range or does not name
        /// a member of an enum, and 401/403 through the shared result handler.
        /// </returns>
        /// <remarks>
        /// Read only, and that decides what it is not guarded by. There is no antiforgery pair
        /// to present - the method changes nothing, so there is no state for a cross-site form
        /// submission to alter; no captcha, because the expensive step this API protects is key
        /// derivation and this path performs none; and no attempt window, because a page of rows
        /// from an index costs about as much as rendering the page that asked for it. The
        /// session is the whole of its authorization, and it is enough: the account identifier
        /// comes from the token, so a caller can only ever read its own history and no role
        /// separates one visitor from another here.
        ///
        /// The bounds are refused rather than clamped. Silently turning a request for 500 rows
        /// into 100 would answer with a page whose items do not match the page size the caller
        /// was told it got. The same rule covers the search term and the two sort parameters: a
        /// nonsense <c>sortBy</c> is not quietly answered as <c>visitedAt</c>, because a caller
        /// that asked to order by something should be told it does not exist rather than be
        /// handed a different order than the one it asked for.
        ///
        /// Search and sort live here rather than in the browser for one reason: the pager shows
        /// a <c>total</c>, and a filter applied after the rows have arrived would count what the
        /// filter is about to remove. The result would be a pager offering four pages of five
        /// rows when only one exists.
        ///
        /// <c>sortBy</c> and <c>sortOrder</c> are read as plain text rather than bound to the
        /// enums, deliberately. Enum binding takes a member name spelled in full, so the
        /// conventional <c>sortOrder=desc</c> would be refused as a bad value while
        /// <c>sortOrder=Descending</c> would be accepted - an API that answers a request as
        /// conventional as this one with a 400 teaches its callers to spell things oddly. They
        /// are parsed below instead, which is also where the sentence naming the values a
        /// client may send comes from; the framework's own message would say only that the
        /// value was wrong, not what would have been right.
        /// </remarks>
        [HttpGet("sessions")]
        [ProducesResponseType<SessionHistoryResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetSessions(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = SessionHistoryLimits.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] string sortBy = "visitedAt",
            [FromQuery] string sortOrder = "desc",
            CancellationToken cancellationToken = default)
        {
            NoStore();

            var field = ParseSortField(sortBy);
            var order = ParseSortOrder(sortOrder);

            if (page < 1 ||
                pageSize is < 1 or > SessionHistoryLimits.MaxPageSize ||
                (search?.Length ?? 0) > SessionHistoryLimits.MaxSearchLength ||
                field is null ||
                order is null)
            {
                return ApiResults.ValidationFailed(
                    SessionHistoryErrors(page, pageSize, search, field, order));
            }

            if (User.UserId() is not { } userId)
            {
                return ApiResults.Anonymous();
            }

            var history = await _sessionHistory.GetHistoryAsync(
                userId,
                page,
                pageSize,
                search,
                field.Value,
                order.Value,
                User.SessionId(),
                cancellationToken);

            if (history is null)
            {
                // The same refusal the profile gives a blocked account, with a sentence of its
                // own: `ProfileResponses.Describe` words this code as a change that was
                // refused, and nothing here was being changed. The code is the contract and
                // stays identical, so a client branching on `account_not_active` behaves the
                // same either way - but the English fallback a human reads should not claim an
                // edit nobody attempted. One helper now answers for the history, the active
                // sessions and the two revoke endpoints, so the sentence cannot drift between
                // them.
                return ApiResults.AccountNotActive(AccountNotActiveMessage);
            }

            return Ok(new SessionHistoryResponse
            {
                Items = history.Visits
                    .Select(visit => new SessionVisitResponse
                    {
                        Id = visit.Id.ToString(),
                        Ip = visit.Ip,
                        CountryCode = visit.CountryCode,
                        AutonomousSystemNumber = visit.AutonomousSystemNumber,
                        Organization = visit.Organization,
                        UserAgent = visit.UserAgent,
                        VisitedAt = visit.VisitedAt,
                        IsCurrent = visit.IsCurrent
                    })
                    .ToArray(),
                Total = history.Total,
                Page = history.Page,
                PageSize = history.PageSize
            });
        }

        /// <summary>
        /// Reports every session the account currently holds: one row per sign-in, described
        /// by the freshest live refresh token of its chain.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with the sessions, 401 and 403 through the shared result handler.</returns>
        /// <remarks>
        /// Read only, and guarded exactly like the visit history: no antiforgery pair (nothing
        /// is changed), no captcha (no key derivation), no attempt window (an index read costs
        /// about what the page that asked for it costs), and the session is the whole of the
        /// authorization - the account comes from the token, so a caller can only ever read its
        /// own sessions and no role separates one visitor from another here.
        ///
        /// No paging and no search parameters, unlike the history: an account holds a handful
        /// of sessions at most, so a page number would be a parameter with nothing behind it -
        /// and the "end all but this one" button revokes by exclusion, which needs the whole
        /// list to exist in one answer.
        ///
        /// The rows are sessions rather than tokens, which is the deliberate opposite of the
        /// history above it: that page lists every rotation so a network change is visible,
        /// this one lists what is still signed in so a person can end what they do not
        /// recognise. A rotation is not a second session; it is the same one still running,
        /// now seen from wherever it refreshed from.
        /// </remarks>
        [HttpGet("active-sessions")]
        [ProducesResponseType<ActiveSessionsResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetActiveSessions(CancellationToken cancellationToken)
        {
            NoStore();

            if (User.UserId() is not { } userId)
            {
                return ApiResults.Anonymous();
            }

            var list = await _activeSessions.GetActiveAsync(
                userId,
                User.SessionId(),
                cancellationToken);

            if (list is null)
            {
                return ApiResults.AccountNotActive(AccountNotActiveMessage);
            }

            return Ok(new ActiveSessionsResponse
            {
                Items = list.Sessions
                    .Select(session => new ActiveSessionResponse
                    {
                        Id = session.Id.ToString(),
                        Ip = session.Ip,
                        CountryCode = session.CountryCode,
                        AutonomousSystemNumber = session.AutonomousSystemNumber,
                        Organization = session.Organization,
                        UserAgent = session.UserAgent,
                        LastSeenAt = session.LastSeenAt,
                        IsCurrent = session.IsCurrent
                    })
                    .ToArray()
            });
        }

        /// <summary>
        /// Ends every session of the account except the one this request arrived with.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 with how many sessions were ended (zero when the account held only this one),
        /// 400 when the antiforgery token is missing or stale, 401 and 403 through the shared
        /// result handler.
        /// </summary>
        /// <remarks>
        /// CSRF-guarded like every write of this API, and nothing else: no captcha (the
        /// expensive step this API protects on unauthenticated paths is key derivation, and a
        /// signed-in caller proving a session already cleared that), no attempt window (ending
        /// sessions is a security act a person reaches for when alarmed, and a window would
        /// make the second alarm of the day a refusal), no password (the session is the proof
        /// of ownership - the same rule sign-out follows, and a password here would mean an
        /// attacker holding a session could not be locked out without typing a password they
        /// may well know).
        ///
        /// The spared session comes from the token's own session claim, never from a
        /// parameter: a body naming "which session to keep" would let a caller end the very
        /// session the request is authenticated with, and the account would be signed out by
        /// its own button in the middle of the click that pressed it. The route carries no id
        /// for the same reason - this endpoint is one act, not one per session.
        ///
        /// The count is of sessions, not of refresh tokens: a session that never rotated holds
        /// one live row, and so does one that did - the rotation revokes the row it replaces in
        /// the same save. The two would agree even if the service reported the other number,
        /// but "sessions ended" is the sentence the button on the page is already making.
        /// </remarks>
        [HttpDelete("active-sessions/others")]
        [ProducesResponseType<RevokeOtherSessionsResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> RevokeOtherSessions(CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (User.UserId() is not { } userId)
            {
                return ApiResults.Anonymous();
            }

            // Ending "every session but the current one" is undefined without a current one.
            // A token this application minted always carries the claim, so reaching this with
            // it missing is the same case `Anonymous` already answers: a token that is not
            // this application's own.
            if (User.SessionId() is not { } currentSession)
            {
                return ApiResults.Anonymous();
            }

            if (await _profileService.GetProfileAsync(userId, cancellationToken) is null)
            {
                return ApiResults.AccountNotActive(AccountNotActiveMessage);
            }

            var revokedCount = await _tokenService.RevokeAllExceptCurrentAsync(
                userId,
                currentSession,
                cancellationToken);

            return Ok(SessionResponses.OtherSessionsRevoked(revokedCount));
        }

        /// <summary>
        /// Ends one session of the account, so that no refresh token in its chain can be
        /// exchanged again.
        /// </summary>
        /// <param name="sessionId">Session to end, as listed by the active sessions page.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 with the session ended, 400 for the session this request arrived with or a
        /// missing antiforgery token, 401 and 403 through the shared result handler, 404 when
        /// the id names nothing this account holds.
        /// </returns>
        /// <remarks>
        /// The current session is refused before any database work and before the ownership
        /// question is asked: the caller named the very session the request is authenticated
        /// with, and the answer to that is a fact about the request rather than about a row.
        /// The refusal is 400 rather than 404 because the session plainly exists - it is the
        /// one the caller is using - and a page whose own row says "cannot end this one" would
        /// be contradicted by a 404 claiming otherwise. The button on the page is disabled for
        /// this row; this refusal is what a direct call minding the UI does not get.
        ///
        /// Every other id that ends in 404 is one answer: unknown, already ended, expired, or
        /// belonging to another account. The four are indistinguishable on purpose - the id is
        /// a guessable value, and a difference in the refusal would turn this endpoint into a
        /// probe for other accounts' session rows. The ownership check itself lives in the
        /// token service's predicate rather than in a separate read, so a session of another
        /// account simply matches nothing.
        ///
        /// The access token of an ended session cannot be recalled - it is signed and self
        /// contained - so it keeps working until it expires, bounded by
        /// <c>Auth:AccessLifetime</c>. Ending a session ends what keeps it alive: no refresh
        /// can be minted from the chain any more.
        /// </remarks>
        [HttpDelete("active-sessions/{sessionId:guid}")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RevokeActiveSession(
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (User.UserId() is not { } userId)
            {
                return ApiResults.Anonymous();
            }

            if (User.SessionId() is { } currentSession && currentSession == sessionId)
            {
                return StatusCode(
                    StatusCodes.Status400BadRequest,
                    SessionResponses.CannotRevokeCurrent());
            }

            if (await _profileService.GetProfileAsync(userId, cancellationToken) is null)
            {
                return ApiResults.AccountNotActive(AccountNotActiveMessage);
            }

            var revoked = await _tokenService.RevokeUserSessionAsync(
                userId,
                sessionId,
                cancellationToken);

            if (!revoked)
            {
                return StatusCode(
                    StatusCodes.Status404NotFound,
                    SessionResponses.NotFound("No such session on this account."));
            }

            return Ok(SessionResponses.Revoked());
        }

        /// <summary>
        /// Turns the text a client sent into the field it names, or nothing at all.
        /// </summary>
        /// <remarks>
        /// Compared after lowering, so <c>IP</c> and <c>VisitedAt</c> are the same request as
        /// their lowercase spelling - a parameter that is read case insensitively by every
        /// conventional API should not be the one exception here. The accepted values are also
        /// the only two the table can honestly order by, which is why the list is a switch and
        /// not a fallback: the third column anybody would want to sort on, the country and the
        /// provider, is resolved from GeoIP while the page is built and is not in the row at all.
        /// </remarks>
        private static SessionSortField? ParseSortField(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "visitedat" => SessionSortField.VisitedAt,
                "ip" => SessionSortField.Ip,
                _ => null
            };

        /// <summary>Turns <c>asc</c> / <c>desc</c> into an order, or nothing at all.</summary>
        private static SessionSortOrder? ParseSortOrder(string? value)
            => SortOrderParser.Parse(
                value,
                SessionSortOrder.Ascending,
                SessionSortOrder.Descending);

        /// <summary>
        /// Which of the bounds a request broke, keyed by the camelCase name of the query
        /// parameter so the reason lands under the input a client would fix.
        /// </summary>
        /// <remarks>
        /// A parameter that named no value gets the sentence listing the values that exist
        /// rather than a generic "not valid": the fastest way to fix a wrong <c>sortBy</c> is
        /// being told which two names are accepted.
        /// </remarks>
        private static Dictionary<string, string[]> SessionHistoryErrors(
            int page,
            int pageSize,
            string? search,
            SessionSortField? field,
            SessionSortOrder? order)
        {
            var errors = ListQueryErrors.Common(
                page,
                pageSize,
                search,
                SessionHistoryLimits.MaxPageSize,
                SessionHistoryLimits.MaxSearchLength);

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

    }
}
