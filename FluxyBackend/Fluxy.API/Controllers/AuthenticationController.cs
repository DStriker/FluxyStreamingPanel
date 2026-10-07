using Fluxy.API.Configuration;
using Fluxy.API.Contracts;
using Fluxy.Application.Services.Authentication;
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
        /// <summary>Code reported when a client spent its sign-in attempts for this window.</summary>
        public const string LoginThrottledCode = AuthenticationResponses.LoginThrottledCode;

        /// <summary>Code reported when a client spent its refreshes for this window.</summary>
        public const string RefreshThrottledCode = AuthenticationResponses.RefreshThrottledCode;

        /// <summary>
        /// Code reported when a refresh token was already spent, or the session it belonged to is
        /// gone. The visitor is sent to the sign-in page.
        /// </summary>
        public const string SessionExpiredCode = AuthenticationResponses.SessionExpiredCode;

        /// <summary>Code reported after a session was ended. Not an error.</summary>
        public const string SignedOutCode = AuthenticationResponses.SignedOutCode;

        private readonly IAuthenticationService _authenticationService;
        private readonly ITokenService _tokenService;
        private readonly ITokenRevocationStore _revocations;
        private readonly IRecaptchaValidator _captchaValidator;
        private readonly IOptionsMonitor<AuthenticationOptions> _authOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationController"/> class.
        /// </summary>
        /// <param name="authenticationService">Service that checks the credentials.</param>
        /// <param name="tokenService">Service that issues, rotates and ends sessions.</param>
        /// <param name="revocations">Store of tokens withdrawn before they expired.</param>
        /// <param name="captchaValidator">Check that a request came from a person.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="throttle">Counter that limits how often one client may try.</param>
        /// <param name="rateLimits">Live limits, so a configuration reload applies without a restart.</param>
        /// <param name="authOptions">Live token settings and landing paths.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public AuthenticationController(
            IAuthenticationService authenticationService,
            ITokenService tokenService,
            ITokenRevocationStore revocations,
            IRecaptchaValidator captchaValidator,
            IAntiforgery antiforgery,
            IAttemptThrottle throttle,
            IOptionsMonitor<RateLimitOptions> rateLimits,
            IOptionsMonitor<AuthenticationOptions> authOptions,
            ILogger<AuthenticationController> logger)
            : base(antiforgery, throttle, rateLimits, logger)
        {
            _authenticationService = authenticationService;
            _tokenService = tokenService;
            _revocations = revocations;
            _captchaValidator = captchaValidator;
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
                var limits = RateLimits.CurrentValue;
                var policy = new AttemptPolicy(limits.RefreshLimit, limits.RefreshWindow);

                if (Throttle.RecordAttempt(RefreshThrottleKey(clientAddress), policy) > limits.RefreshLimit)
                {
                    return Throttled(RefreshThrottledCode, limits.RefreshWindow);
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
                        Code = SessionExpiredCode,
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
        /// Two things are withdrawn rather than one. The refresh chain is revoked in the
        /// database, so no new token can be minted from it, and the access token's identifier is
        /// blocked for the rest of its life, so the token in the browser stops working at once
        /// rather than in up to five minutes. Without the second half, "sign out" would be a
        /// request rather than a fact for as long as the access token lasted, and a shared
        /// machine would keep working for anybody who came back to it.
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

            var sessionId = ReadSessionClaim();
            var tokenId = ReadTokenIdClaim();
            var remaining = RemainingAccessTokenLifetime();

            if (sessionId is { } session)
            {
                var revoked = await _tokenService.RevokeSessionAsync(session, cancellationToken);

                Logger.LogInformation(
                    "Ended session {SessionId} of account {UserId}; {Revoked} refresh token(s) " +
                    "were still live.",
                    session,
                    ReadSubjectClaim(),
                    revoked ? 1 : 0);
            }

            // The access token cannot be recalled, but it can be refused. The entry is written
            // for exactly as long as the token would otherwise have worked, so there is no
            // cleanup to do and nothing outlives the token it names.
            await _revocations.BlockAsync(tokenId, remaining, cancellationToken);

            AuthCookies.Clear(HttpContext);

            return Ok(new MessageResponse
            {
                Code = SignedOutCode,
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
                UserId = ReadSubjectClaim(),
                Username = ReadNameClaim(),
                Role = ReadRoleClaim().ToString()
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
            var limits = RateLimits.CurrentValue;
            var username = request.Username?.Trim() ?? string.Empty;

            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (!await PassesCaptchaAsync(_captchaValidator, captchaAction, clientAddress, cancellationToken))
            {
                return CaptchaRefused();
            }

            // Throttled by role and remote address only. A single IP cannot brute-force
            // logins for a given entrance without hitting the window.
            var policy = new AttemptPolicy(limits.LoginLimit, limits.LoginWindow);
            var key = LoginThrottleKey(requiredRole, clientAddress);

            // Recorded before the comparison rather than after. Counting afterwards would let a
            // caller make as many of the expensive comparisons as they liked before the first
            // refusal was noticed. Keyed by role and address only, so password-guessing is
            // throttled per IP per entrance.
            if (Throttle.RecordAttempt(key, policy) > limits.LoginLimit)
            {
                Logger.LogInformation(
                    "Refused a sign-in for role {Role} from {ClientAddress}: the limit of {Limit} " +
                    "attempts per window is spent.",
                    requiredRole,
                    clientAddress,
                    limits.LoginLimit);

                return Throttled(LoginThrottledCode, limits.LoginWindow);
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
            Throttle.ResetAttempts(key);

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

        /// <summary>
        /// How much longer the presented access token would have worked.
        /// </summary>
        /// <remarks>
        /// Taken from the token's own expiry claim rather than from the elapsed time since it was
        /// issued, because a token read from a cookie may be minutes old by the time a request
        /// carries it - and a withdrawal shorter than the token's real remaining life would let
        /// the token start working again after a sign-out. A token that has already expired
        /// yields nothing, which is right: there is nothing left to withdraw.
        /// </remarks>
        private TimeSpan RemainingAccessTokenLifetime()
        {
            var expires = ReadExpiryClaim();

            if (expires is not { } moment)
            {
                return TimeSpan.Zero;
            }

            var remaining = moment - DateTimeOffset.UtcNow;

            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        private string? ReadRefreshCookie()
            => Request.Cookies.TryGetValue(AuthenticationOptions.RefreshCookieName, out var token)
                ? token
                : null;

        private string? ReadSubjectClaim()
            => User.FindFirst(TokenClaimTypes.Subject)?.Value;

        private string? ReadNameClaim()
            => User.FindFirst(TokenClaimTypes.Name)?.Value ?? User.Identity?.Name;

        private Guid? ReadSessionClaim()
            => Guid.TryParse(User.FindFirst(TokenClaimTypes.Session)?.Value, out var session)
                ? session
                : null;

        private string? ReadTokenIdClaim()
            => User.FindFirst(TokenClaimTypes.TokenId)?.Value;

        private DateTimeOffset? ReadExpiryClaim()
        {
            var value = User.FindFirst(TokenClaimTypes.ExpiresAt)?.Value;

            return long.TryParse(value, out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
        }

        private UserRole ReadRoleClaim()
        {
            var value = User.FindFirst(TokenClaimTypes.Role)?.Value;

            return int.TryParse(value, out var role) && Enum.IsDefined(typeof(UserRole), role)
                ? (UserRole)role
                : UserRole.Client;
        }

        /// <summary>
        /// A router path for a role, with the leading slash the frontend hands straight to its
        /// router.
        /// </summary>
        private string LandingPath(UserRole role)
            => "/" + _authOptions.CurrentValue.LandingPathOf(role).TrimStart('/');

        private string LoginPath()
            => "/" + _authOptions.CurrentValue.ClientLoginPath.TrimStart('/');

        private static string LoginThrottleKey(UserRole role, string clientAddress)
            => $"login:{role}:{clientAddress}";

        private static string RefreshThrottleKey(string clientAddress)
            => $"refresh:{clientAddress}";
    }
}