using Fluxy.API.Configuration;
using Fluxy.API.Contracts;
using Fluxy.Application.Services.Authentication;
using Fluxy.Application.Services.Registration;
using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Fluxy.API.Controllers
{
    /// <summary>
    /// Everything an unauthenticated visitor may do: fetch an antiforgery token, ask for a
    /// registration, and confirm it with the code that was mailed out.
    /// </summary>
    /// <remarks>
    /// The controller owns the transport and nothing else. It decides which check runs first,
    /// hands the values to a service and turns what comes back into a status code, a code and a
    /// sentence. It holds no registration rule: a value that is wrong is rejected by
    /// <c>RegistrationService</c>, and if a rule ever changes this class is not where it changes.
    ///
    /// It is separate from the sign-in controller because the two answer different questions and
    /// share almost nothing: registration writes a row and mails it a code, sign-in reads a row
    /// and opens a session. What they do share is the three guards in
    /// <see cref="AuthControllerBase"/>, which is exactly what that base class is for.
    ///
    /// The route carries no <c>api</c> segment. This process serves nothing but the API, so a
    /// prefix that distinguishes it from a website is a segment every caller has to type without
    /// telling anybody anything - and it becomes one more thing to change when the API is moved
    /// behind a path of its own, which a reverse proxy or gateway tends to add anyway.
    /// </remarks>
    [ApiController]
    [Route("auth")]
    [Produces("application/json")]
    public sealed class RegistrationController : AuthControllerBase
    {
        /// <summary>
        /// Action a registration token has to have been minted for. The frontend asks for it by
        /// name, so the two have to agree, and this is the backend half of that agreement.
        /// </summary>
        public const string RegisterCaptchaAction = "register";

        /// <summary>
        /// Action a confirmation token has to have been minted for. A frontend that writes the
        /// confirmation form has to ask for exactly this name, the same way the registration form
        /// asks for <see cref="RegisterCaptchaAction"/>.
        /// </summary>
        public const string ConfirmCaptchaAction = "register_confirm";

        /// <summary>Code reported when a client already spent its registration for this window.</summary>
        public const string RegisterThrottledCode = "registration_rate_limited";

        /// <summary>Code reported when a client spent its confirmation attempts for this window.</summary>
        public const string ConfirmThrottledCode = "confirmation_rate_limited";

        private readonly IRegistrationService _registrationService;
        private readonly IRecaptchaValidator _captchaValidator;
        private readonly ITokenService _tokenService;
        private readonly IOptionsMonitor<AuthenticationOptions> _authOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistrationController"/> class.
        /// </summary>
        /// <param name="registrationService">Service that owns the registration rules.</param>
        /// <param name="captchaValidator">Check that a request came from a person.</param>
        /// <param name="tokenService">Service that opens the session a confirmation earns.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="throttle">Counter that limits how often one client may try.</param>
        /// <param name="rateLimits">
        /// Live limits, so a configuration reload changes them without a restart.
        /// </param>
        /// <param name="authOptions">Live token settings and landing paths.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public RegistrationController(
            IRegistrationService registrationService,
            IRecaptchaValidator captchaValidator,
            ITokenService tokenService,
            IAntiforgery antiforgery,
            IAttemptThrottle throttle,
            IOptionsMonitor<RateLimitOptions> rateLimits,
            IOptionsMonitor<AuthenticationOptions> authOptions,
            ILogger<RegistrationController> logger)
            : base(antiforgery, throttle, rateLimits, logger)
        {
            _registrationService = registrationService;
            _captchaValidator = captchaValidator;
            _tokenService = tokenService;
            _authOptions = authOptions;
        }

        /// <summary>
        /// Hands out the token a browser has to send with any request that changes something.
        /// </summary>
        /// <remarks>
        /// The same value goes into the body and into a cookie the browser is allowed to read.
        /// Writing both is deliberate: the cookie is what lets a single page application read the
        /// token synchronously on every submit, and the body is what a client that cannot see the
        /// cookie falls back to. The frontend prefers the cookie and reads this body only when
        /// there is none.
        ///
        /// It is the one cookie in this application that a script is meant to read. It cannot be
        /// used to do anything on its own - it is half of a pair whose other half is
        /// <c>HttpOnly</c> - so exposing it costs nothing, while hiding it would mean a
        /// round-trip to this endpoint before every single POST.
        ///
        /// A token is bound to the identity that was current when it was minted, and that is
        /// why the copy is not a lasting convenience: a token read while anonymous is refused
        /// once the same request carries a signed-in user (and the other way round), with
        /// <c>"meant for a different claims-based user"</c> as the reason. A client that signs
        /// in or out must therefore ask for a token again rather than reuse the one it already
        /// has - which is why every submit in the frontend refetches, and why the copy is
        /// dropped from the cookie whenever the session changes (see <c>AuthCookies</c>).
        /// </remarks>
        /// <returns>The token, with the readable cookie set alongside it.</returns>
        [HttpGet("csrf")]
        [ProducesResponseType<CsrfTokenResponse>(StatusCodes.Status200OK)]
        public IActionResult GetCsrfToken()
        {
            // Also writes the framework's own cookie, which holds the secret half of the pair.
            var tokenSet = Antiforgery.GetAndStoreTokens(HttpContext);

            if (string.IsNullOrEmpty(tokenSet.RequestToken))
            {
                // The token set is built from the antiforgery cookie, so this cannot happen unless
                // the services are not registered. Answering with the framework's own problem
                // response keeps the endpoint from pretending to have succeeded.
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "The antiforgery services are not registered.");
            }

            Response.Cookies.Append(
                AntiforgeryExtensions.TokenCookieName,
                tokenSet.RequestToken,
                new CookieOptions
                {
                    // Readable on purpose: this is the copy the frontend reads.
                    HttpOnly = false,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps,
                    Path = "/"
                });

            return Ok(new CsrfTokenResponse { Token = tokenSet.RequestToken });
        }

        /// <summary>
        /// Registers a new account and mails the code that confirms it.
        /// </summary>
        /// <param name="request">
        /// Values the visitor supplied. Nothing on it is null by the time the action runs, because
        /// a body that failed validation never reaches an action.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 when the account was stored, 400 for a bad body or a refused captcha, 409 for a
        /// taken value, 502 when the code could not be mailed, and 503 on an installation that has
        /// no mail server.
        /// </returns>
        [HttpPost("register")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status502BadGateway)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequest request,
            CancellationToken cancellationToken)
        {
            var clientAddress = ClientAddress;
            var limits = RateLimits.CurrentValue;
            var policy = new AttemptPolicy(limits.RegisterLimit, limits.RegisterWindow);

            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (!await PassesCaptchaAsync(_captchaValidator, RegisterCaptchaAction, clientAddress, cancellationToken))
            {
                return CaptchaRefused();
            }

            // Peeked, never spent. A body that turns out to be rejected or a value that is taken
            // costs nothing, and a visitor who mistypes a username can correct it immediately
            // instead of waiting out a window. The permit is only spent on the line below, once a
            // row really exists.
            var limit = limits.RegisterLimit;

            if (Throttle.GetAttempts(RegisterThrottleKey(clientAddress), policy) >= limit)
            {
                Logger.LogInformation(
                    "Refused a registration from {ClientAddress}: the limit of {Limit} " +
                    "registrations per window is spent.",
                    clientAddress,
                    limit);

                return Throttled(RegisterThrottledCode, limits.RegisterWindow);
            }

            RegistrationOutcome outcome;

            try
            {
                outcome = await _registrationService.RegisterAsync(
                    new NewRegistration
                    {
                        Username = request.Username!,
                        Email = request.Email!,
                        Password = request.Password!
                    },
                    cancellationToken);
            }
            catch (DuplicateRegistrationException exception)
            {
                // The lookup the service did passed, and then another request took the same
                // username or email first. That is an ordinary outcome of two people choosing the
                // same name at the same moment, not a fault, so it becomes the same refusal the
                // loser would have got from a check that had not raced.
                Logger.LogInformation(
                    exception,
                    "A registration lost the race for a unique value and was refused.");

                outcome = new RegistrationOutcome { Status = RegistrationStatus.AlreadyExists };
            }

            if (outcome.RowPersisted)
            {
                Throttle.RecordAttempt(RegisterThrottleKey(clientAddress), policy);
            }

            return Respond(outcome);
        }

        /// <summary>
        /// Confirms a pending registration with the code that was mailed to the address, and
        /// signs the visitor in on the strength of it.
        /// </summary>
        /// <param name="request">Address and code the visitor supplied.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 once the account is confirmed and a session is open, 400 when the code is wrong or
        /// has expired, and 429 once the client has spent its attempts for this window.
        /// </returns>
        /// <remarks>
        /// The session is issued here rather than leaving the visitor to sign in a moment later.
        /// Confirming a registration is the one moment where the server has just proved the
        /// visitor controls a mailbox and holds a password they chose, which is precisely what
        /// signing in proves; making them type both again immediately afterwards would be asking
        /// for the same proof twice, and it would train people to expect a second form right
        /// after a successful one.
        ///
        /// The tokens are the same two cookies a sign-in writes, produced by the same service,
        /// so nothing downstream can tell a confirmed session from a signed-in one.
        /// </remarks>
        [HttpPost("register/confirm")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Confirm(
            [FromBody] ConfirmRegistrationRequest request,
            CancellationToken cancellationToken)
        {
            var clientAddress = ClientAddress;
            var limits = RateLimits.CurrentValue;
            var policy = new AttemptPolicy(limits.ConfirmLimit, limits.ConfirmWindow);

            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (!await PassesCaptchaAsync(_captchaValidator, ConfirmCaptchaAction, clientAddress, cancellationToken))
            {
                return CaptchaRefused();
            }

            // Recorded before the code is compared, not after, and that order is the point.
            // Verifying a code costs as much as hashing a password does, so counting afterwards
            // would let a caller make as many of those comparisons as they liked before the first
            // one was noticed. Here the attempt is already over the limit by the time the
            // comparison would start, so the expensive work is unreachable.
            var limit = limits.ConfirmLimit;

            if (Throttle.RecordAttempt(ConfirmThrottleKey(clientAddress), policy) > limit)
            {
                Logger.LogInformation(
                    "Refused a confirmation from {ClientAddress}: the limit of {Limit} attempts " +
                    "per window is spent.",
                    clientAddress,
                    limit);

                return Throttled(ConfirmThrottledCode, limits.ConfirmWindow);
            }

            var outcome = await _registrationService.ConfirmAsync(
                request.Email!,
                request.Code!,
                cancellationToken);

            if (outcome.ConfirmedAccount is { } account)
            {
                var tokens = await _tokenService.IssueAsync(
                    account,
                    clientAddress,
                    Request.Headers.UserAgent.FirstOrDefault(),
                    cancellationToken);

                AuthCookies.Write(HttpContext, tokens);

                Logger.LogInformation(
                    "Confirmed the registration of {Username} and opened a session for it.",
                    account.Username);

                return Ok(new MessageResponse
                {
                    Code = RegistrationResponses.ConfirmedCode,
                    Message = "Your account is confirmed. You are now signed in.",
                    Redirect = "/" + _authOptions.CurrentValue
                        .LandingPathOf(account.Role).TrimStart('/')
                });
            }

            return Respond(outcome);
        }

        private static string RegisterThrottleKey(string clientAddress)
            => $"register:{clientAddress}";

        private static string ConfirmThrottleKey(string clientAddress)
            => $"confirm:{clientAddress}";

        /// <summary>
        /// Renders what a service reported, normalizing the field names on the way out so that a
        /// client sees one shape whatever rejected the request.
        /// </summary>
        private IActionResult Respond(RegistrationOutcome outcome)
            => StatusCode(
                RegistrationResponses.StatusCodeOf(outcome.Status),
                RegistrationResponses.Describe(
                    outcome.Status,
                    FieldErrorKeys.FromPropertyNames(outcome.Errors)));
    }
}