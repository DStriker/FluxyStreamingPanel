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
    /// Reading the profile of a signed-in account and changing it through a single PATCH:
    /// username, email, password, login guard or display time zone.
    /// </summary>
    /// <remarks>
    /// The controller owns transport and nothing else: which check runs in which order, what
    /// status each outcome gets, and the session mechanics that follow a change - the username
    /// lives in the access token, so the token in the browser has to be replaced at the moment
    /// the name is, and a changed password has to end every other session the account holds.
    /// The rules themselves are <c>ProfileService</c>; if one of them changes, this class is not
    /// where it changes.
    ///
    /// The whole controller requires a session, stated once on the class rather than repeated
    /// per action. That attribute runs before anything inside an action does, which is what
    /// turns a caller with no session into a 401 about the request rather than a 400 about a
    /// missing antiforgery token - the same reason <c>logout</c> carries it.
    ///
    /// The profile is edited through one <c>PATCH</c>, not one path per field. This is the REST
    /// shape the repository now asks for (see AGENTS.md, "API design aims at REST"): a profile is
    /// a single resource, so the body says which change is asked for — <c>username</c>,
    /// <c>email</c>, <c>newPassword</c>, <c>loginGuard</c> or <c>timeZone</c> — and the endpoint
    /// refuses a body that names none or more than one rather than guessing an order. One kind
    /// per request is not a lack of PATCH semantics: the staged-confirmation flow holds exactly
    /// one pending change at a time, so a body naming two would silently overwrite the first
    /// staging before it was ever confirmed.
    ///
    /// The four changes that touch what the account <em>is</em> share one attempt window rather
    /// than having one each. They are the same act against the same account, and separate
    /// windows would simply be more guesses for a caller who is willing to rotate a field name.
    /// The time zone is the exception: it gets its own window and skips the captcha, because it
    /// saves on selection and would otherwise spend the permits the password change is counting
    /// on — and it is applied at once, so nothing about it is worth a captcha check.
    ///
    /// The route carries no <c>api</c> segment, for the reason the registration controller
    /// gives, and stays under <c>auth</c> so the frontend's dev proxy reaches it without being
    /// taught a new prefix.
    /// </remarks>
    [ApiController]
    [Route("auth")]
    [Authorize]
    [Produces("application/json")]
    public sealed class ProfileController : AuthControllerBase
    {
        /// <summary>
        /// Action a change token has to have been minted for. A frontend asks for it by name,
        /// so the two have to agree, and this is the backend half of that agreement.
        /// </summary>
        public const string ChangeCaptchaAction = "profile_change";

        /// <summary>Action a confirmation token has to have been minted for.</summary>
        public const string ConfirmCaptchaAction = "profile_confirm";

        /// <summary>Code reported when a client spent its change attempts for this window.</summary>
        public const string ChangeThrottledCode = "profile_rate_limited";

        /// <summary>Code reported when a client spent its confirmation attempts for this window.</summary>
        public const string ConfirmThrottledCode = "profile_confirmation_rate_limited";

        /// <summary>
        /// Code reported when the caller asked to end the session the request arrived with.
        /// </summary>
        public const string CannotRevokeCurrentCode = "cannot_revoke_current";

        /// <summary>
        /// Code reported when a session id names nothing the account holds - never, gone, or
        /// belonging to somebody else. The three are one answer on purpose: a caller must not
        /// be able to tell a session that is gone from one that was never theirs, because the
        /// id is a guessable value and the difference would turn the endpoint into a probe.
        /// </summary>
        public const string SessionNotFoundCode = "session_not_found";

        /// <summary>Code reported after a session was ended at the account's own request.</summary>
        public const string SessionRevokedCode = "session_revoked";

        /// <summary>Code reported after every session but the current one was ended.</summary>
        public const string OtherSessionsRevokedCode = "other_sessions_revoked";

        private readonly IProfileService _profileService;
        private readonly IRecaptchaValidator _captchaValidator;
        private readonly ITokenService _tokenService;
        private readonly IGeoIpResolver _geoIpResolver;
        private readonly ISessionHistoryService _sessionHistory;
        private readonly IActiveSessionService _activeSessions;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfileController"/> class.
        /// </summary>
        /// <param name="profileService">Service that owns the profile rules.</param>
        /// <param name="captchaValidator">Check that a request came from a person.</param>
        /// <param name="tokenService">Service that ends and reopens sessions after a change.</param>
        /// <param name="geoIpResolver">Resolver that places the caller on the map for the lookup.</param>
        /// <param name="sessionHistory">Reader of the account's stored sessions.</param>
        /// <param name="activeSessions">Reader of the account's live sessions.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="throttle">Counter that limits how often one client may try.</param>
        /// <param name="rateLimits">Live limits, so a configuration reload applies without a restart.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public ProfileController(
            IProfileService profileService,
            IRecaptchaValidator captchaValidator,
            ITokenService tokenService,
            IGeoIpResolver geoIpResolver,
            ISessionHistoryService sessionHistory,
            IActiveSessionService activeSessions,
            IAntiforgery antiforgery,
            IAttemptThrottle throttle,
            IOptionsMonitor<RateLimitOptions> rateLimits,
            ILogger<ProfileController> logger)
            : base(antiforgery, throttle, rateLimits, logger)
        {
            _profileService = profileService;
            _captchaValidator = captchaValidator;
            _tokenService = tokenService;
            _geoIpResolver = geoIpResolver;
            _sessionHistory = sessionHistory;
            _activeSessions = activeSessions;
        }

        /// <summary>
        /// Reports the username, email, role, time zone and login guard of the account behind
        /// the presented token.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with the profile, or 401 and 403 through the shared result handler.</returns>
        [HttpGet("profile")]
        [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            if (ReadSubjectClaim() is not { } userId)
            {
                return Anonymous();
            }

            var profile = await _profileService.GetProfileAsync(userId, cancellationToken);
            var guard = await _profileService.GetLoginGuardAsync(userId, cancellationToken);

            if (profile is null || guard is null)
            {
                return StatusCode(
                    ProfileResponses.StatusCodeOf(ProfileChangeStatus.AccountNotActive),
                    ProfileResponses.Describe(ProfileChangeStatus.AccountNotActive));
            }

            return Ok(new ProfileResponse
            {
                Username = profile.Username,
                Email = profile.Email,
                Role = profile.Role.ToString(),
                TimeZone = profile.TimeZone,
                GeoProtectionEnabled = guard.GeoProtectionEnabled,
                BindSessionToIp = guard.BindSessionToIp,
                AllowedIps = guard.AllowedIps,
                AllowedCountry = guard.AllowedCountry,
                AllowedAutonomousSystemNumber = guard.AllowedAutonomousSystemNumber
            });
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
            var field = ParseSortField(sortBy);
            var order = ParseSortOrder(sortOrder);

            if (page < 1 ||
                pageSize is < 1 or > SessionHistoryLimits.MaxPageSize ||
                (search?.Length ?? 0) > SessionHistoryLimits.MaxSearchLength ||
                field is null ||
                order is null)
            {
                return StatusCode(
                    StatusCodes.Status400BadRequest,
                    new MessageResponse
                    {
                        Code = "validation_failed",
                        Message = "Some of the values you entered are not valid.",
                        Errors = SessionHistoryErrors(page, pageSize, search, field, order)
                    });
            }

            if (ReadSubjectClaim() is not { } userId)
            {
                return Anonymous();
            }

            var history = await _sessionHistory.GetHistoryAsync(
                userId,
                page,
                pageSize,
                search,
                field.Value,
                order.Value,
                ReadSessionClaim(),
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
                return AccountNotActive();
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
            if (ReadSubjectClaim() is not { } userId)
            {
                return Anonymous();
            }

            var list = await _activeSessions.GetActiveAsync(
                userId,
                ReadSessionClaim(),
                cancellationToken);

            if (list is null)
            {
                return AccountNotActive();
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

            if (ReadSubjectClaim() is not { } userId)
            {
                return Anonymous();
            }

            // Ending "every session but the current one" is undefined without a current one.
            // A token this application minted always carries the claim, so reaching this with
            // it missing is the same case `Anonymous` already answers: a token that is not
            // this application's own.
            if (ReadSessionClaim() is not { } currentSession)
            {
                return Anonymous();
            }

            if (await _profileService.GetProfileAsync(userId, cancellationToken) is null)
            {
                return AccountNotActive();
            }

            var revokedCount = await _tokenService.RevokeAllExceptCurrentAsync(
                userId,
                currentSession,
                cancellationToken);

            return Ok(new RevokeOtherSessionsResponse
            {
                Code = OtherSessionsRevokedCode,
                Message = revokedCount == 0
                    ? "This account held no other sessions."
                    : "The other sessions of this account have been ended.",
                RevokedCount = revokedCount
            });
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

            if (ReadSubjectClaim() is not { } userId)
            {
                return Anonymous();
            }

            if (ReadSessionClaim() is { } currentSession && currentSession == sessionId)
            {
                return CannotRevokeCurrent();
            }

            if (await _profileService.GetProfileAsync(userId, cancellationToken) is null)
            {
                return AccountNotActive();
            }

            var revoked = await _tokenService.RevokeUserSessionAsync(
                userId,
                sessionId,
                cancellationToken);

            if (!revoked)
            {
                return StatusCode(
                    StatusCodes.Status404NotFound,
                    new MessageResponse
                    {
                        Code = SessionNotFoundCode,
                        Message = "No such session on this account."
                    });
            }

            return Ok(new MessageResponse
            {
                Code = SessionRevokedCode,
                Message = "The session has been ended."
            });
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
        private static Dictionary<string, string[]> SessionHistoryErrors(
            int page,
            int pageSize,
            string? search,
            SessionSortField? field,
            SessionSortOrder? order)
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
        /// Reports what the GeoIP database knows about the network the caller arrived from.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with the caller's address, country and provider, each nullable but the address.</returns>
        /// <remarks>
        /// Exists for the "allow my current network" button on the profile page: a visitor
        /// cannot be expected to know their own autonomous system number. A plain GET behind
        /// the session, with no guards of its own - it changes nothing and a local database
        /// lookup costs nothing worth throttling.
        /// </remarks>
        [HttpGet("geo/lookup")]
        [ProducesResponseType<GeoLookupResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> LookupGeo(CancellationToken cancellationToken)
        {
            var geo = await _geoIpResolver.ResolveAsync(ClientAddress, cancellationToken);

            return Ok(new GeoLookupResponse
            {
                Ip = geo.Ip ?? ClientAddress,
                CountryCode = geo.CountryCode,
                AutonomousSystemNumber = geo.AutonomousSystemNumber,
                Organization = geo.Organization
            });
        }

        /// <summary>
        /// Applies the one change a body names: a new login name, a new address, a new password,
        /// a new login guard, or a display time zone.
        /// </summary>
        /// <param name="request">The change, as exactly one field group.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 when the change is on the account or its code is on the way, 400 for a body naming
        /// no change or more than one, a refused captcha, a wrong password or a value this system
        /// does not know, 403 when the account may not change itself, 409 for a taken value, 429
        /// once the attempts are spent, 502 when the code could not be mailed.
        /// </returns>
        /// <remarks>
        /// One endpoint rather than the five paths it replaced: a profile is one resource, and
        /// REST asks for one method on it — see the class remarks for why exactly one change
        /// kind may appear in a body and why the refusal for two names both rather than picking
        /// an order.
        ///
        /// The two halves run under the rules they have always had, because the rules are about
        /// what the change costs and what it proves, not about the path it arrived on. The four
        /// sensitive changes go through <see cref="ChangeAsync"/>: CSRF, captcha, the shared
        /// attempt window, then the service. The time zone keeps its own lighter path — CSRF and
        /// its own window, no captcha — because it is applied at once and a session is the whole
        /// of the proof it needs, and because it would otherwise spend the permits the password
        /// change counts on. Which half runs is decided by the shape of the body alone.
        /// </remarks>
        [HttpPatch("profile")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> Patch(
            [FromBody] PatchProfileRequest request,
            CancellationToken cancellationToken)
        {
            var errors = PatchShapeErrors(request);

            if (errors.Count > 0)
            {
                return StatusCode(
                    StatusCodes.Status400BadRequest,
                    new MessageResponse
                    {
                        Code = "validation_failed",
                        Message = "Some of the values you entered are not valid.",
                        Errors = errors
                    });
            }

            if (SensesTheAccount(request))
            {
                return await ChangeAsync(
                    request.CurrentPassword ?? string.Empty,
                    (userId, currentPassword) =>
                        ApplyAsync(request, userId, currentPassword, cancellationToken),
                    cancellationToken);
            }

            var clientAddress = ClientAddress;
            var limits = RateLimits.CurrentValue;
            var policy = new AttemptPolicy(limits.PreferenceLimit, limits.PreferenceWindow);

            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (Throttle.RecordAttempt(TimeZoneThrottleKey(clientAddress), policy) > limits.PreferenceLimit)
            {
                Logger.LogInformation(
                    "Refused a time zone change from {ClientAddress}: the limit of {Limit} " +
                    "attempts per window is spent.",
                    clientAddress,
                    limits.PreferenceLimit);

                return Throttled(ChangeThrottledCode, limits.PreferenceWindow);
            }

            if (ReadSubjectClaim() is not { } userId)
            {
                return Anonymous();
            }

            var outcome = await _profileService.UpdateTimeZoneAsync(
                userId,
                request.TimeZone,
                cancellationToken);

            // Nothing in this outcome touches a token - the time zone is not a claim - so the
            // rendering half of RespondAsync is all that runs for it, which is exactly why it
            // may be called here rather than the status being rendered a second time.
            return await RespondAsync(outcome, clientAddress, cancellationToken);
        }

        /// <summary>
        /// Which of the two rules a body broke: it must name exactly one change, and every
        /// change but the time zone must carry the password that authorizes it.
        /// </summary>
        /// <remarks>
        /// Keyed by the camelCase name of the field a client would fix — <c>currentPassword</c>
        /// for the missing password, so the refusal lands under the input that caused it — or by
        /// <c>profile</c> for the shape itself, which belongs to no single input on the form.
        /// The time zone is exempt from the password because it changes how dates are shown to
        /// the person holding the session rather than what the account is; the same exemption
        /// every one of its predecessors had.
        /// </remarks>
        private static Dictionary<string, string[]> PatchShapeErrors(PatchProfileRequest request)
        {
            var kinds = (request.Username is null ? 0 : 1)
                + (request.Email is null ? 0 : 1)
                + (request.NewPassword is null ? 0 : 1)
                + (request.LoginGuard is null ? 0 : 1)
                + (request.TimeZone is null ? 0 : 1);

            var errors = new Dictionary<string, string[]>();

            if (kinds != 1)
            {
                errors["profile"] =
                    ["Send exactly one change: username, email, newPassword, loginGuard or timeZone."];
            }

            if (SensesTheAccount(request) && string.IsNullOrEmpty(request.CurrentPassword))
            {
                errors["currentPassword"] = ["A password is required to change the account."];
            }

            return errors;
        }

        /// <summary>Whether the body asks to change what the account is rather than how it renders.</summary>
        private static bool SensesTheAccount(PatchProfileRequest request)
            => request.Username is not null
            || request.Email is not null
            || request.NewPassword is not null
            || request.LoginGuard is not null;

        /// <summary>
        /// Hands the named change to the service that owns its rules. Exactly one kind is
        /// present by the time this runs — <see cref="PatchShapeErrors"/> has already refused
        /// any other shape — so the first matching arm is the only arm that runs.
        /// </summary>
        private Task<ProfileChangeOutcome> ApplyAsync(
            PatchProfileRequest request,
            Guid userId,
            string currentPassword,
            CancellationToken cancellationToken)
            => request switch
            {
                { Username: { } username } => _profileService.ChangeUsernameAsync(
                    userId,
                    currentPassword,
                    username,
                    cancellationToken),

                { Email: { } email } => _profileService.ChangeEmailAsync(
                    userId,
                    currentPassword,
                    email,
                    cancellationToken),

                { NewPassword: { } newPassword } => _profileService.ChangePasswordAsync(
                    userId,
                    currentPassword,
                    newPassword,
                    cancellationToken),

                { LoginGuard: { } guard } => _profileService.ChangeLoginGuardAsync(
                    userId,
                    currentPassword,
                    new LoginGuardSettings
                    {
                        GeoProtectionEnabled = guard.GeoProtectionEnabled,
                        BindSessionToIp = guard.BindSessionToIp,
                        AllowedIps = guard.AllowedIps ?? [],
                        AllowedCountry = guard.AllowedCountry,
                        AllowedAutonomousSystemNumber = guard.AllowedAutonomousSystemNumber
                    },
                    ClientAddress,
                    cancellationToken),

                _ => throw new InvalidOperationException(
                    "A profile PATCH on the sensitive path must name username, email, " +
                    "newPassword or loginGuard.")
            };


        /// <summary>
        /// Applies the change whose code was mailed to the account.
        /// </summary>
        /// <param name="request">The code.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 once the change is on the account, 400 for a wrong or expired code, 429 once the attempts are spent.</returns>
        [HttpPost("profile/confirm")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Confirm(
            [FromBody] ConfirmProfileChangeRequest request,
            CancellationToken cancellationToken)
        {
            var clientAddress = ClientAddress;
            var limits = RateLimits.CurrentValue;
            var policy = new AttemptPolicy(limits.LoginLimit, limits.LoginWindow);

            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (!await PassesCaptchaAsync(_captchaValidator, ConfirmCaptchaAction, clientAddress, cancellationToken))
            {
                return CaptchaRefused();
            }

            // Recorded before the code is compared, not after: verifying a code costs as much as
            // hashing a password does, so counting afterwards would let a caller spend as many
            // of those comparisons as it liked before the first one was noticed.
            if (Throttle.RecordAttempt(ConfirmThrottleKey(clientAddress), policy) > limits.LoginLimit)
            {
                Logger.LogInformation(
                    "Refused a profile confirmation from {ClientAddress}: the limit of {Limit} " +
                    "attempts per window is spent.",
                    clientAddress,
                    limits.LoginLimit);

                return Throttled(ConfirmThrottledCode, limits.LoginWindow);
            }

            if (ReadSubjectClaim() is not { } userId)
            {
                return Anonymous();
            }

            var outcome = await _profileService.ConfirmAsync(
                userId,
                request.Code ?? string.Empty,
                cancellationToken);

            return await RespondAsync(outcome, clientAddress, cancellationToken);
        }

        /// <summary>
        /// The body the sensitive half of <c>PATCH /auth/profile</c> runs: the three guards,
        /// then the service, then whatever the outcome costs in session terms.
        /// </summary>
        /// <remarks>
        /// The order is the one <see cref="AuthControllerBase"/> states: antiforgery, captcha,
        /// limit, then the work. The attempt is counted before the service is called rather than
        /// on success, because the expensive step of this path is comparing the current password
        /// - and a caller that is going to be refused must not reach it.
        /// </remarks>
        private async Task<IActionResult> ChangeAsync(
            string currentPassword,
            Func<Guid, string, Task<ProfileChangeOutcome>> change,
            CancellationToken cancellationToken)
        {
            var clientAddress = ClientAddress;
            var limits = RateLimits.CurrentValue;
            var policy = new AttemptPolicy(limits.LoginLimit, limits.LoginWindow);

            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (!await PassesCaptchaAsync(_captchaValidator, ChangeCaptchaAction, clientAddress, cancellationToken))
            {
                return CaptchaRefused();
            }

            if (Throttle.RecordAttempt(ChangeThrottleKey(clientAddress), policy) > limits.LoginLimit)
            {
                Logger.LogInformation(
                    "Refused a profile change from {ClientAddress}: the limit of {Limit} " +
                    "attempts per window is spent.",
                    clientAddress,
                    limits.LoginLimit);

                return Throttled(ChangeThrottledCode, limits.LoginWindow);
            }

            if (ReadSubjectClaim() is not { } userId)
            {
                return Anonymous();
            }

            var outcome = await change(userId, currentPassword);

            return await RespondAsync(outcome, clientAddress, cancellationToken);
        }

        /// <summary>
        /// Renders an outcome and, when it changed something that lives in a token, brings the
        /// tokens in the browser in line with it.
        /// </summary>
        /// <remarks>
        /// Two changes and two answers, both of them about what the token in the browser now
        /// says:
        ///
        /// A new username has to reach the page without waiting for the access token to expire,
        /// so the session that carries the old name is ended and a fresh pair is written. Any
        /// other device holding this account picks the new name up on its own next refresh,
        /// within one access token lifetime.
        ///
        /// A new password ends every session of the account before the pair is reissued, because
        /// whoever asked for the old password may be holding a session opened with it. The
        /// browser that made the change is signed straight back in, so the person who changed it
        /// notices nothing; every other device has to sign in again. Access tokens already in
        /// flight keep working until they expire - they cannot be recalled - which is bounded by
        /// <c>Auth:AccessLifetime</c>.
        ///
        /// A new email changes no claim at all, so nothing here runs for it.
        ///
        /// A new login guard is the same: it names networks rather than secrets, so the
        /// sessions the account holds are left alone and the refresh path rechecks each of
        /// them against the new lists on its next rotation.
        /// </remarks>
        private async Task<IActionResult> RespondAsync(
            ProfileChangeOutcome outcome,
            string clientAddress,
            CancellationToken cancellationToken)
        {
            if (outcome is { Account: { } account, Kind: { } kind }
                && outcome.Status is ProfileChangeStatus.Applied or ProfileChangeStatus.Confirmed)
            {
                switch (kind)
                {
                    case PendingChangeKind.ChangePassword:
                        await _tokenService.RevokeAllSessionsAsync(account.Id, cancellationToken);
                        break;

                    case PendingChangeKind.ChangeUsername when ReadSessionClaim() is { } session:
                        await _tokenService.RevokeSessionAsync(session, cancellationToken);
                        break;
                }

                if (kind is PendingChangeKind.ChangeUsername or PendingChangeKind.ChangePassword)
                {
                    var tokens = await _tokenService.IssueAsync(
                        account,
                        clientAddress,
                        Request.Headers.UserAgent.FirstOrDefault(),
                        cancellationToken);

                    AuthCookies.Write(HttpContext, tokens);
                }
            }

            return StatusCode(
                ProfileResponses.StatusCodeOf(outcome.Status),
                ProfileResponses.Describe(
                    outcome.Status,
                    FieldErrorKeys.FromPropertyNames(outcome.Errors)));
        }

        /// <summary>
        /// The refusal for a token that carries no account identifier. <c>[Authorize]</c> has
        /// already said the request is authenticated, so a token without a subject is not a
        /// visitor who forgot to sign in - it is a token this application would not have minted,
        /// and answering about the session rather than about a header is the truthful reply.
        /// </summary>
        private IActionResult Anonymous()
            => StatusCode(
                StatusCodes.Status401Unauthorized,
                new MessageResponse
                {
                    Code = "auth_required",
                    Message = "Your session has ended. Please sign in again."
                });

        /// <summary>
        /// The refusal a blocked or missing account gets on every read and every session act
        /// of this controller. The code is the one the profile change flow already answers
        /// with, so a client branching on <c>account_not_active</c> behaves the same way for
        /// an account that lost its standing here as it does on the profile - while the
        /// sentence stays one about the account rather than about an edit nobody attempted.
        /// </summary>
        private IActionResult AccountNotActive()
            => StatusCode(
                ProfileResponses.StatusCodeOf(ProfileChangeStatus.AccountNotActive),
                new MessageResponse
                {
                    Code = "account_not_active",
                    Message = "This account is not active."
                });

        /// <summary>
        /// The refusal for an attempt to end the session the request arrived with. The page
        /// disables the button for its own row; this is what a direct call that ignored the UI
        /// gets instead of a silent no-op, so "I pressed it and nothing happened" cannot be
        /// the state a caller is left in.
        /// </summary>
        private IActionResult CannotRevokeCurrent()
            => StatusCode(
                StatusCodes.Status400BadRequest,
                new MessageResponse
                {
                    Code = CannotRevokeCurrentCode,
                    Message = "The session you are using cannot be ended from here. Sign out instead."
                });

        private Guid? ReadSubjectClaim()
            => Guid.TryParse(User.FindFirst(TokenClaimTypes.Subject)?.Value, out var userId)
                ? userId
                : null;

        private Guid? ReadSessionClaim()
            => Guid.TryParse(User.FindFirst(TokenClaimTypes.Session)?.Value, out var session)
                ? session
                : null;

        private static string ChangeThrottleKey(string clientAddress)
            => $"login:profile:{clientAddress}";

        /// <summary>
        /// A window of its own, separate from <see cref="ChangeThrottleKey"/>: the time zone
        /// endpoint saves on selection and would otherwise drain the permits a password change
        /// is counting on.
        /// </summary>
        private static string TimeZoneThrottleKey(string clientAddress)
            => $"profile_timezone:{clientAddress}";

        private static string ConfirmThrottleKey(string clientAddress)
            => $"profile_confirm:{clientAddress}";
    }
}
