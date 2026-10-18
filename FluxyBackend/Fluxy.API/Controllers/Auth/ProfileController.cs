using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Profile;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Security;
using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Authentication;
using Fluxy.Core.Models.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Auth
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

        private readonly IProfileService _profileService;
        private readonly CaptchaGuard _captchaGuard;
        private readonly AttemptLimiter _limiter;
        private readonly ITokenService _tokenService;
        private readonly IGeoIpResolver _geoIpResolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfileController"/> class.
        /// </summary>
        /// <param name="profileService">Service that owns the profile rules.</param>
        /// <param name="captchaGuard">Check that a request came from a person.</param>
        /// <param name="limiter">Counter that limits how often one client may try.</param>
        /// <param name="tokenService">Service that ends and reopens sessions after a change.</param>
        /// <param name="geoIpResolver">Resolver that places the caller on the map for the lookup.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public ProfileController(
            IProfileService profileService,
            CaptchaGuard captchaGuard,
            AttemptLimiter limiter,
            ITokenService tokenService,
            IGeoIpResolver geoIpResolver,
            IAntiforgery antiforgery,
            ILogger<ProfileController> logger)
            : base(antiforgery, logger)
        {
            _profileService = profileService;
            _captchaGuard = captchaGuard;
            _limiter = limiter;
            _tokenService = tokenService;
            _geoIpResolver = geoIpResolver;
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
            NoStore();

            if (User.UserId() is not { } userId)
            {
                return ApiResults.Anonymous();
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
                Permissions = UserPermissionCatalog.All
                    .Where(profile.Permissions.Contains)
                    .Select(UserPermissionCatalog.NameOf)
                    .ToArray(),
                TimeZone = profile.TimeZone,
                GeoProtectionEnabled = guard.GeoProtectionEnabled,
                BindSessionToIp = guard.BindSessionToIp,
                AllowedIps = guard.AllowedIps,
                AllowedCountry = guard.AllowedCountry,
                AllowedAutonomousSystemNumber = guard.AllowedAutonomousSystemNumber
            });
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
            NoStore();

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
            var limits = _limiter.Limits;
            var policy = new AttemptPolicy(limits.PreferenceLimit, limits.PreferenceWindow);

            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (_limiter.Record(ThrottleKeys.ProfileTimeZone(clientAddress), policy) > limits.PreferenceLimit)
            {
                Logger.LogInformation(
                    "Refused a time zone change from {ClientAddress}: the limit of {Limit} " +
                    "attempts per window is spent.",
                    clientAddress,
                    limits.PreferenceLimit);

                return _limiter.Throttled(Response, ChangeThrottledCode, limits.PreferenceWindow);
            }

            if (User.UserId() is not { } userId)
            {
                return ApiResults.Anonymous();
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
            var limits = _limiter.Limits;
            var policy = new AttemptPolicy(limits.LoginLimit, limits.LoginWindow);

            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (await _captchaGuard.RejectsAsync(
                    Request, ConfirmCaptchaAction, clientAddress, cancellationToken) is { } captchaFailure)
            {
                return captchaFailure;
            }

            // Recorded before the code is compared, not after: verifying a code costs as much as
            // hashing a password does, so counting afterwards would let a caller spend as many
            // of those comparisons as it liked before the first one was noticed.
            if (_limiter.Record(ThrottleKeys.ProfileConfirm(clientAddress), policy) > limits.LoginLimit)
            {
                Logger.LogInformation(
                    "Refused a profile confirmation from {ClientAddress}: the limit of {Limit} " +
                    "attempts per window is spent.",
                    clientAddress,
                    limits.LoginLimit);

                return _limiter.Throttled(Response, ConfirmThrottledCode, limits.LoginWindow);
            }

            if (User.UserId() is not { } userId)
            {
                return ApiResults.Anonymous();
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
            var limits = _limiter.Limits;
            var policy = new AttemptPolicy(limits.LoginLimit, limits.LoginWindow);

            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (await _captchaGuard.RejectsAsync(
                    Request, ChangeCaptchaAction, clientAddress, cancellationToken) is { } captchaFailure)
            {
                return captchaFailure;
            }

            if (_limiter.Record(ThrottleKeys.ProfileChange(clientAddress), policy) > limits.LoginLimit)
            {
                Logger.LogInformation(
                    "Refused a profile change from {ClientAddress}: the limit of {Limit} " +
                    "attempts per window is spent.",
                    clientAddress,
                    limits.LoginLimit);

                return _limiter.Throttled(Response, ChangeThrottledCode, limits.LoginWindow);
            }

            if (User.UserId() is not { } userId)
            {
                return ApiResults.Anonymous();
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

                    case PendingChangeKind.ChangeUsername when User.SessionId() is { } session:
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
    }
}
