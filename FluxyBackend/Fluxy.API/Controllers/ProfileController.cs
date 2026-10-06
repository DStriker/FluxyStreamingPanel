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
    /// Reading the profile of a signed-in account, changing its username, email or password,
    /// and setting the time zone it is displayed in.
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
    /// The three change endpoints share one attempt window rather than having one each. They
    /// are the same act against the same account, and three separate windows would simply be
    /// three times the guesses for a caller who is willing to rotate a path. The time zone
    /// endpoint is the exception: it gets its own window, because it saves on selection and
    /// would otherwise spend the permits the three changes are counting on.
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

        private readonly IProfileService _profileService;
        private readonly IRecaptchaValidator _captchaValidator;
        private readonly ITokenService _tokenService;
        private readonly IGeoIpResolver _geoIpResolver;
        private readonly ISessionHistoryService _sessionHistory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfileController"/> class.
        /// </summary>
        /// <param name="profileService">Service that owns the profile rules.</param>
        /// <param name="captchaValidator">Check that a request came from a person.</param>
        /// <param name="tokenService">Service that ends and reopens sessions after a change.</param>
        /// <param name="geoIpResolver">Resolver that places the caller on the map for the lookup.</param>
        /// <param name="sessionHistory">Reader of the account's stored sessions.</param>
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
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 with the page, 400 when a bound was not a number or is out of range, and 401/403
        /// through the shared result handler.
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
        /// was told it got.
        /// </remarks>
        [HttpGet("sessions")]
        [ProducesResponseType<SessionHistoryResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetSessions(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = SessionHistoryLimits.DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            if (page < 1 || pageSize is < 1 or > SessionHistoryLimits.MaxPageSize)
            {
                return StatusCode(
                    StatusCodes.Status400BadRequest,
                    new MessageResponse
                    {
                        Code = "validation_failed",
                        Message = "Some of the values you entered are not valid.",
                        Errors = SessionHistoryErrors(page, pageSize)
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
                ReadSessionClaim(),
                cancellationToken);

            if (history is null)
            {
                // The same refusal the profile gives a blocked account, with a sentence of its
                // own: `Describe` words this code as a change that was refused, and nothing
                // here was being changed. The code is the contract and stays identical, so a
                // client branching on `account_not_active` behaves the same either way - but
                // the English fallback a human reads should not claim an edit nobody attempted.
                return StatusCode(
                    ProfileResponses.StatusCodeOf(ProfileChangeStatus.AccountNotActive),
                    new MessageResponse
                    {
                        Code = "account_not_active",
                        Message = "This account is not active."
                    });
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
        /// Which of the two bounds a request broke, keyed by the camelCase name of the query
        /// parameter so the reason lands under the input a client would fix.
        /// </summary>
        private static Dictionary<string, string[]> SessionHistoryErrors(int page, int pageSize)
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
        /// Sets the display time zone of the account, or clears it.
        /// </summary>
        /// <remarks>
        /// The only endpoint here that applies at once: a time zone decides how dates are
        /// shown to the person holding the session rather than what the account is, so there
        /// is nothing an email code could prove that the session has not already. It is still
        /// behind the antiforgery pair and behind an attempt window of its own - not the one
        /// the three changes share, because a page that saves on selection would otherwise
        /// spend the permits a password change is counting on, and the other way round.
        ///
        /// No captcha: it is called by a signed-in visitor from a form the application
        /// rendered, and the cost of a refused request is a lookup rather than key
        /// derivation, which is what the captcha on the other three exists for.
        /// </remarks>
        /// <param name="request">The IANA identifier, or nothing to return to the browser's own.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 once the choice is on the account, 400 for an identifier this system does not
        /// know, 403 when the account may not change itself, 429 once the attempts are spent.
        /// </returns>
        [HttpPost("profile/timezone")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> ChangeTimeZone(
            [FromBody] ChangeTimezoneRequest request,
            CancellationToken cancellationToken)
        {
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
        /// Starts changing the login name of the account.
        /// </summary>
        /// <param name="request">Current password and the new name.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 when the change is on the account or its code is on the way, 400 for a refused
        /// captcha or a wrong password, 403 when the account may not change itself, 409 for a
        /// taken value, 429 once the attempts are spent, 502 when the code could not be mailed.
        /// </returns>
        [HttpPost("profile/username")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status502BadGateway)]
        public Task<IActionResult> ChangeUsername(
            [FromBody] ChangeUsernameRequest request,
            CancellationToken cancellationToken)
            => ChangeAsync(
                request.CurrentPassword ?? string.Empty,
                (userId, currentPassword) => _profileService.ChangeUsernameAsync(
                    userId,
                    currentPassword,
                    request.Username ?? string.Empty,
                    cancellationToken),
                cancellationToken);

        /// <summary>
        /// Starts changing the email address of the account. The code goes to the new address,
        /// because that is the one whose ownership the change has to prove.
        /// </summary>
        /// <param name="request">Current password and the new address.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The same outcomes as <see cref="ChangeUsername"/>.</returns>
        [HttpPost("profile/email")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status502BadGateway)]
        public Task<IActionResult> ChangeEmail(
            [FromBody] ChangeEmailRequest request,
            CancellationToken cancellationToken)
            => ChangeAsync(
                request.CurrentPassword ?? string.Empty,
                (userId, currentPassword) => _profileService.ChangeEmailAsync(
                    userId,
                    currentPassword,
                    request.Email ?? string.Empty,
                    cancellationToken),
                cancellationToken);

        /// <summary>
        /// Starts changing the password of the account. The new one replaces the old only when
        /// the mailed code is entered, and doing so ends every session the account holds.
        /// </summary>
        /// <param name="request">Current password and the new one.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The same outcomes as <see cref="ChangeUsername"/>.</returns>
        [HttpPost("profile/password")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status502BadGateway)]
        public Task<IActionResult> ChangePassword(
            [FromBody] ChangePasswordRequest request,
            CancellationToken cancellationToken)
            => ChangeAsync(
                request.CurrentPassword ?? string.Empty,
                (userId, currentPassword) => _profileService.ChangePasswordAsync(
                    userId,
                    currentPassword,
                    request.NewPassword ?? string.Empty,
                    cancellationToken),
                cancellationToken);

        /// <summary>
        /// Starts changing the login guard of the account: the two switches and the three
        /// allow lists. The new guard replaces the old one only when the mailed code is
        /// entered, and it never touches the sessions the account holds.
        /// </summary>
        /// <param name="request">Current password and the requested guard.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The same outcomes as <see cref="ChangeUsername"/>.</returns>
        /// <remarks>
        /// Shares the attempt window of the other three changes: it is the same act against
        /// the same account, and a separate window would be more guesses for a caller willing
        /// to rotate a path. Unlike a password change it ends no session, because a guard
        /// names networks rather than secrets - whoever holds a session keeps it until the
        /// refresh path rechecks the network it arrives from.
        /// </remarks>
        [HttpPost("profile/geo")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status502BadGateway)]
        public Task<IActionResult> ChangeLoginGuard(
            [FromBody] ChangeLoginGuardRequest request,
            CancellationToken cancellationToken)
            => ChangeAsync(
                request.CurrentPassword ?? string.Empty,
                (userId, currentPassword) => _profileService.ChangeLoginGuardAsync(
                    userId,
                    currentPassword,
                    new LoginGuardSettings
                    {
                        GeoProtectionEnabled = request.GeoProtectionEnabled,
                        BindSessionToIp = request.BindSessionToIp,
                        AllowedIps = request.AllowedIps ?? [],
                        AllowedCountry = request.AllowedCountry,
                        AllowedAutonomousSystemNumber = request.AllowedAutonomousSystemNumber
                    },
                    ClientAddress,
                    cancellationToken),
                cancellationToken);

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
        /// The body every change endpoint runs: the three guards, then the service, then
        /// whatever the outcome costs in session terms.
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
