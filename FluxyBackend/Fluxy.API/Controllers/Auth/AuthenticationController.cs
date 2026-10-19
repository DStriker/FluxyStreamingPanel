using Fluxy.API.Contracts.Authentication;
using Fluxy.API.Contracts.Common;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Security;
using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Authentication;
using Fluxy.Core.Models.Users;
using Fluxy.Core.Security.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Fluxy.API.Controllers.Auth
{
    /// <summary>
    /// Signing in, staying signed in, and signing out.
    /// </summary>
    /// <remarks>
    /// The controller owns transport and nothing else. It decides which check runs in which
    /// order, turns a service outcome into a status code and a code, writes the cookies, and
    /// names the page the visitor belongs on. It holds no authentication rule: whether a
    /// password is correct is <c>AuthenticationService</c>, whether a role may enter an area is
    /// <c>RoleRequirementHandler</c>, and the moment a token stops working is the handler's own
    /// lifetime. If any of those rules change, this class is not where they change.
    ///
    /// The endpoints are one per role rather than one parameterized by role. A single
    /// <c>POST /auth/login?role=admin</c> would put the audience in a query string, where a
    /// stale bookmark or a mistyped URL sends somebody to the wrong door, and where a client
    /// could try the other two. With one path per form, each entrance is a fixed thing that
    /// cannot be varied, and a form cannot be pointed at an audience it was not built for.
    ///
    /// The route carries no <c>api</c> segment, for the reason the registration controller gives.
    /// </remarks>
    [ApiController]
    [Route("auth")]
    [Produces("application/json")]
    public sealed class AuthenticationController : AuthControllerBase
    {
        private readonly IAuthenticationService _authenticationService;
        private readonly ITokenService _tokenService;
        private readonly CaptchaGuard _captchaGuard;
        private readonly AttemptLimiter _limiter;
        private readonly IOptionsMonitor<AuthenticationOptions> _authOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationController"/> class.
        /// </summary>
        /// <param name="authenticationService">Service that checks the credentials.</param>
        /// <param name="tokenService">Service that issues, rotates and ends sessions.</param>
        /// <param name="captchaGuard">Check that a request came from a person.</param>
        /// <param name="limiter">Counter that limits how often one client may try.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="authOptions">Live token settings and landing paths.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public AuthenticationController(
            IAuthenticationService authenticationService,
            ITokenService tokenService,
            CaptchaGuard captchaGuard,
            AttemptLimiter limiter,
            IAntiforgery antiforgery,
            IOptionsMonitor<AuthenticationOptions> authOptions,
            ILogger<AuthenticationController> logger)
            : base(antiforgery, logger)
        {
            _authenticationService = authenticationService;
            _tokenService = tokenService;
            _captchaGuard = captchaGuard;
            _limiter = limiter;
            _authOptions = authOptions;
        }

        /// <summary>
        /// Signs a client in at the client form.
        /// </summary>
        /// <param name="request">Username and password as typed.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 with the tokens written to cookies and the path the client belongs on, 401 for any
        /// refused set of credentials, and 429 once the attempts are spent.
        /// </returns>
        [HttpPost("client-login")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status429TooManyRequests)]
        public Task<IActionResult> ClientLogin(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken)
            => SignInAsync(UserRole.Client, "client_login", request, cancellationToken);

        /// <summary>
        /// Signs a reseller in at the reseller form.
        /// </summary>
        /// <param name="request">Username and password as typed.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with the tokens, 401 for any refusal, 429 once the attempts are spent.</returns>
        [HttpPost("reseller-login")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status429TooManyRequests)]
        public Task<IActionResult> ResellerLogin(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken)
            => SignInAsync(UserRole.Reseller, "reseller_login", request, cancellationToken);

        /// <summary>
        /// Signs an operator in at the operator form.
        /// </summary>
        /// <param name="request">Username and password as typed.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with the tokens, 401 for any refusal, 429 once the attempts are spent.</returns>
        [HttpPost("admin-login")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status429TooManyRequests)]
        public Task<IActionResult> AdminLogin(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken)
            => SignInAsync(UserRole.Admin, "admin_login", request, cancellationToken);

        /// <summary>
        /// Exchanges a refresh token for a new pair, without asking for the password again.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 with a fresh pair in the cookies, 401 when the presented token was already spent
        /// or the session is gone, and 429 once the refreshes are spent.
        /// </returns>
        /// <remarks>
        /// The refresh token is read from its cookie rather than from the body, which is what
        /// lets the endpoint be a plain <c>POST</c> with nothing in it. Presenting a token that
        /// has already been used revokes the whole session, so a copied refresh token cannot be
        /// spent twice: the first use wins and the second one ends the chain for both.
        ///
        /// A browser that retries a request it already got an answer to presents the same token
        /// twice, and pays a sign-in for it. That is the price of the guarantee, and it is the
        /// cheap direction to pay in - the alternative is a stolen token that can be used for as
        /// long as nobody notices.
        /// </remarks>
        [HttpPost("refresh")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
        {
            var clientAddress = ClientAddress;

            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var refreshToken = ReadRefreshCookie();

            // A request with no cookie spends no permit: there is nothing to rotate, so the
            // work this window exists to cap is never reached. Counting it anyway would let a
            // browser that never had a session drain the window just by asking - the frontend
            // refreshes after any 401 it cannot answer otherwise, and a guest page loading
            // `/auth/me` is exactly that, once per navigation.
            if (refreshToken is not null)
            {
                var limits = _limiter.Limits;
                var policy = new AttemptPolicy(limits.RefreshLimit, limits.RefreshWindow);

                if (_limiter.Record(ThrottleKeys.Refresh(clientAddress), policy) > limits.RefreshLimit)
                {
                    return _limiter.Throttled(
                        Response, AuthenticationResponses.RefreshThrottledCode, limits.RefreshWindow);
                }
            }

            var outcome = await _tokenService.RefreshAsync(
                refreshToken,
                clientAddress,
                Request.Headers.UserAgent.FirstOrDefault(),
                cancellationToken);

            if (outcome.Status is not RefreshStatus.Refreshed || outcome.Tokens is null)
            {
                // Cleared whatever happened, and this is not a detail. A refresh token the
                // server will not accept is worse than no token at all: leaving it in the
                // browser would have the client retry with a value that can never work, and the
                // visitor would sit on a page that silently never loads.
                AuthCookies.Clear(HttpContext);

                return StatusCode(
                    StatusCodes.Status401Unauthorized,
                    new MessageResponse
                    {
                        Code = AuthenticationResponses.SessionExpiredCode,
                        Message = "Your session has ended. Please sign in again.",
                        Redirect = LoginPath()
                    });
            }

            AuthCookies.Write(HttpContext, outcome.Tokens);

            Logger.LogInformation(
                "Refreshed the session of account {UserId} in role {Role}.",
                outcome.UserId,
                outcome.Role);

            return Ok(new MessageResponse
            {
                Code = AuthenticationResponses.AuthenticatedCode,
                Message = "Session refreshed.",
                Redirect = LandingPath(outcome.Role)
            });
        }

        /// <summary>
        /// Ends the current session and clears the tokens from the browser.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with the session ended, or 401 when there is no session to end.</returns>
        /// <remarks>
        /// One endpoint for every role, on purpose. Signing out is not a decision about what an
        /// account may do, so it has nothing to be separated by - three identical endpoints
        /// would differ only in a path, and a client would have to know its role to choose one
        /// in order to stop being signed in.
        ///
        /// It requires a session, and the attribute is the whole mechanism rather than
        /// ceremony. Without it the action ran for any caller, and the first check inside -
        /// antiforgery - answered an unauthenticated request with <c>csrf_invalid</c>, which
        /// names a missing form token when the truthful answer is "there is no session here
        /// to end". The refusal now comes from the authorization layer before any check inside
        /// the action: 401 <c>auth_required</c>, a code about the state of the request rather
        /// than about a header. An access token that has merely expired is not the case this
        /// turns away - a browser refreshes the pair first and the sign-out then arrives with
        /// a token that is still good, so what this refuses is a caller with nothing to end.
        ///
        /// Two things are withdrawn rather than one, and both are owned by the token service.
        /// The refresh chain is revoked in the database, so no new token can be minted from it,
        /// and the session is recorded in the revocation store, so every access token it ever
        /// held stops working at once rather than in up to five minutes. Without the second
        /// half, "sign out" would be a request rather than a fact for as long as the access
        /// token lasted, and a shared machine would keep working for anybody who came back to
        /// it.
        /// </remarks>
        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var sessionId = User.SessionId();

            if (sessionId is { } session)
            {
                var revoked = await _tokenService.RevokeSessionAsync(session, cancellationToken);

                Logger.LogInformation(
                    "Ended session {SessionId} of account {UserId}; a live refresh row was " +
                    "{Revoked}.",
                    session,
                    User.Subject(),
                    revoked ? "present" : "absent");
            }

            AuthCookies.Clear(HttpContext);

            return Ok(new MessageResponse
            {
                Code = AuthenticationResponses.SignedOutCode,
                Message = "Signed out.",
                Redirect = LoginPath()
            });
        }

        /// <summary>
        /// Reports the account behind the presented token, for a client that wants to know who it
        /// is already holding a session for.
        /// </summary>
        /// <returns>200 with the account, or 401 and 403 through the shared result handler.</returns>
        /// <remarks>
        /// It exists because the tokens are in cookies the browser will not let script read. A
        /// frontend cannot ask the cookie who the user is or what role it holds; it has to ask
        /// the server. The alternative - reading the token in JavaScript - would undo the reason
        /// it is in an <c>HttpOnly</c> cookie.
        ///
        /// The role reported is the one the token carries, which the authorization handler has
        /// already compared against the account row for every request that reaches a protected
        /// endpoint. This endpoint asks for no role in particular, so there is no comparison to
        /// make and nothing here to re-check.
        /// </remarks>
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        public IActionResult Me()
        {
            NoStore();

            return Ok(new SessionResponse
            {
                UserId = User.Subject(),
                Username = User.Name(),
                Role = User.Role().ToString()
            });
        }

        /// <summary>
        /// The body of every sign-in, whatever the role.
        /// </summary>
        /// <remarks>
        /// The order of the checks is the design and is stated once here rather than three
        /// times over: antiforgery, then captcha, then limit, then the password. The password
        /// comparison is the expensive step - verifying a BCrypt hash costs about as much as
        /// producing one - and it is the last thing to run, so the checks that cost nothing put
        /// it out of reach of a caller who has neither a session of the page nor the time to
        /// spend.
        /// </remarks>
        private async Task<IActionResult> SignInAsync(
            UserRole requiredRole,
            string captchaAction,
            LoginRequest request,
            CancellationToken cancellationToken)
        {
            var clientAddress = ClientAddress;
            var limits = _limiter.Limits;
            var username = request.Username?.Trim() ?? string.Empty;

            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (await _captchaGuard.RejectsAsync(
                    Request, captchaAction, clientAddress, cancellationToken) is { } captchaFailure)
            {
                return captchaFailure;
            }

            // Throttled by role and remote address only. A single IP cannot brute-force
            // logins for a given entrance without hitting the window.
            var policy = new AttemptPolicy(limits.LoginLimit, limits.LoginWindow);
            var key = ThrottleKeys.Login(requiredRole, clientAddress);

            // Recorded before the comparison rather than after. Counting afterwards would let a
            // caller make as many of the expensive comparisons as they liked before the first
            // refusal was noticed. Keyed by role and address only, so password-guessing is
            // throttled per IP per entrance.
            if (_limiter.Record(key, policy) > limits.LoginLimit)
            {
                Logger.LogInformation(
                    "Refused a sign-in for role {Role} from {ClientAddress}: the limit of {Limit} " +
                    "attempts per window is spent.",
                    requiredRole,
                    clientAddress,
                    limits.LoginLimit);

                return _limiter.Throttled(
                    Response, AuthenticationResponses.LoginThrottledCode, limits.LoginWindow);
            }

            var outcome = await _authenticationService.AuthenticateAsync(
                new NewCredentials
                {
                    Username = username,
                    Password = request.Password ?? string.Empty,
                    RequiredRole = requiredRole,
                    ClientAddress = clientAddress,
                    UserAgent = Request.Headers.UserAgent.FirstOrDefault()
                },
                cancellationToken);

            if (outcome.Status is not AuthenticationStatus.Authenticated || outcome.Tokens is null)
            {
                // One answer for every reason. A wrong password, an unknown name, an unconfirmed
                // account, a blocked one and an account of the wrong role are indistinguishable
                // here on purpose - see AuthenticationService for why that is the point, and
                // AuthenticationResponses for the status and code it becomes.
                return StatusCode(
                    AuthenticationResponses.StatusCodeOf(outcome.Status),
                    AuthenticationResponses.Describe(outcome.Status));
            }

            // Cleared only on success, and only for this key. The failures leading here are
            // forgotten the moment the person proved they are not guessing, so a legitimate
            // visitor who fumbled twice is not left with a window that is nearly spent.
            _limiter.Reset(key);

            AuthCookies.Write(HttpContext, outcome.Tokens);

            Logger.LogInformation(
                "Signed in {Username} in role {Role} from {ClientAddress}.",
                outcome.Username,
                outcome.Role,
                clientAddress);

            return Ok(AuthenticationResponses.Describe(
                outcome.Status,
                outcome.Role,
                _authOptions.CurrentValue));
        }

        private string? ReadRefreshCookie()
            => Request.Cookies.TryGetValue(AuthenticationOptions.RefreshCookieName, out var token)
                ? token
                : null;

        /// <summary>
        /// A router path for a role, with the leading slash the frontend hands straight to its
        /// router.
        /// </summary>
        private string LandingPath(UserRole role)
            => "/" + _authOptions.CurrentValue.LandingPathOf(role).TrimStart('/');

        private string LoginPath()
            => "/" + _authOptions.CurrentValue.ClientLoginPath.TrimStart('/');
    }
}