using System.Globalization;
using Fluxy.API.Configuration;
using Fluxy.API.Contracts;
using Fluxy.Application.Services.Registration;
using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Fluxy.API.Controllers
{
    /// <summary>
    /// Everything an unauthenticated visitor may do: fetch a CSRF token, ask for a
    /// registration, and confirm it with the code that was mailed out.
    /// </summary>
    /// <remarks>
    /// The controller owns the transport and nothing else. It decides which check runs first,
    /// hands the values to a service and turns what comes back into a status code, a code and a
    /// sentence. It holds no registration rule: a value that is wrong is rejected by
    /// <c>RegistrationService</c>, and if a rule ever changes this class is not where it changes.
    /// </remarks>
    [ApiController]
    [Route("api/auth")]
    [Produces("application/json")]
    public sealed class AuthController : ControllerBase
    {
        /// <summary>Header the reCAPTCHA token arrives in.</summary>
        public const string CaptchaHeaderName = "X-Recaptcha-Token";

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

        /// <summary>Code reported when the antiforgery token is missing, stale or does not match.</summary>
        public const string InvalidCsrfCode = "csrf_invalid";

        /// <summary>Code reported when the reCAPTCHA check refuses a request.</summary>
        public const string InvalidCaptchaCode = "captcha_invalid";

        /// <summary>Code reported when a client already spent its registration for this window.</summary>
        public const string RegisterThrottledCode = "registration_rate_limited";

        /// <summary>Code reported when a client spent its confirmation attempts for this window.</summary>
        public const string ConfirmThrottledCode = "confirmation_rate_limited";

        /// <summary>Address used as a throttle key when the connection has none.</summary>
        private const string UnknownClient = "unknown";

        private readonly IRegistrationService _registrationService;
        private readonly IRecaptchaValidator _captchaValidator;
        private readonly IAttemptThrottle _throttle;
        private readonly IAntiforgery _antiforgery;
        private readonly IOptionsMonitor<RateLimitOptions> _rateLimits;
        private readonly ILogger<AuthController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthController"/> class.
        /// </summary>
        /// <param name="registrationService">Service that owns the registration rules.</param>
        /// <param name="captchaValidator">Check that a request came from a person.</param>
        /// <param name="throttle">Counter that limits how often one client may try.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="rateLimits">
        /// Live limits, so a configuration reload changes them without a restart.
        /// </param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public AuthController(
            IRegistrationService registrationService,
            IRecaptchaValidator captchaValidator,
            IAttemptThrottle throttle,
            IAntiforgery antiforgery,
            IOptionsMonitor<RateLimitOptions> rateLimits,
            ILogger<AuthController> logger)
        {
            _registrationService = registrationService;
            _captchaValidator = captchaValidator;
            _throttle = throttle;
            _antiforgery = antiforgery;
            _rateLimits = rateLimits;
            _logger = logger;
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
        /// </remarks>
        /// <returns>The token, with the readable cookie set alongside it.</returns>
        [HttpGet("csrf")]
        [ProducesResponseType<CsrfTokenResponse>(StatusCodes.Status200OK)]
        public IActionResult GetCsrfToken()
        {
            // Also writes the framework's own cookie, which holds the secret half of the pair.
            var tokenSet = _antiforgery.GetAndStoreTokens(HttpContext);

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
            var limits = _rateLimits.CurrentValue;
            var policy = new AttemptPolicy(limits.RegisterLimit, limits.RegisterWindow);

            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (!await PassesCaptchaAsync(RegisterCaptchaAction, clientAddress, cancellationToken))
            {
                return CaptchaRefused();
            }

            // Peeked, never spent. A body that turns out to be rejected or a value that is taken
            // costs nothing, and a visitor who mistypes a username can correct it immediately
            // instead of waiting out a window. The permit is only spent on the line below, once a
            // row really exists.
            var limit = limits.RegisterLimit;

            if (_throttle.GetAttempts(RegisterThrottleKey(clientAddress), policy) >= limit)
            {
                _logger.LogInformation(
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
                _logger.LogInformation(
                    exception,
                    "A registration lost the race for a unique value and was refused.");

                outcome = new RegistrationOutcome { Status = RegistrationStatus.AlreadyExists };
            }

            if (outcome.RowPersisted)
            {
                _throttle.RecordAttempt(RegisterThrottleKey(clientAddress), policy);
            }

            return Respond(outcome);
        }

        /// <summary>
        /// Confirms a pending registration with the code that was mailed to the address.
        /// </summary>
        /// <param name="request">Address and code the visitor supplied.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 once the account is confirmed, 400 when the code is wrong or has expired, and 429
        /// once the client has spent its attempts for this window.
        /// </returns>
        [HttpPost("register/confirm")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Confirm(
            [FromBody] ConfirmRegistrationRequest request,
            CancellationToken cancellationToken)
        {
            var clientAddress = ClientAddress;
            var limits = _rateLimits.CurrentValue;
            var policy = new AttemptPolicy(limits.ConfirmLimit, limits.ConfirmWindow);

            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (!await PassesCaptchaAsync(ConfirmCaptchaAction, clientAddress, cancellationToken))
            {
                return CaptchaRefused();
            }

            // Recorded before the code is compared, not after, and that order is the point.
            // Verifying a code costs as much as hashing a password does, so counting afterwards
            // would let a caller make as many of those comparisons as they liked before the first
            // one was noticed. Here the attempt is already over the limit by the time the
            // comparison would start, so the expensive work is unreachable.
            var limit = limits.ConfirmLimit;

            if (_throttle.RecordAttempt(ConfirmThrottleKey(clientAddress), policy) > limit)
            {
                _logger.LogInformation(
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

            return Respond(outcome);
        }

        /// <summary>
        /// Address the request came from, as this server saw it.
        /// </summary>
        /// <remarks>
        /// Nothing sits in front of the application yet, so this is the address of the socket and
        /// it is exactly the client. Put a proxy in front and this becomes the proxy's address,
        /// and every client would then share one window - which is the failure that shows up as
        /// "everybody is rate limited at once". Reading the forwarded headers is the fix, and it
        /// has to happen in the pipeline as early as possible.
        /// </remarks>
        private string ClientAddress
            => HttpContext.Connection.RemoteIpAddress?.ToString() ?? UnknownClient;

        private static string RegisterThrottleKey(string clientAddress)
            => $"register:{clientAddress}";

        private static string ConfirmThrottleKey(string clientAddress)
            => $"confirm:{clientAddress}";

        /// <summary>
        /// Checks the antiforgery token and, when it does not hold, builds the refusal.
        /// </summary>
        /// <returns>
        /// Null when the request may continue, otherwise the response to answer with.
        /// </returns>
        /// <remarks>
        /// The token is checked here rather than with <c>[ValidateAntiForgeryToken]</c> for two
        /// reasons. That attribute resolves to an MVC filter that only
        /// <c>AddControllersWithViews</c> registers, and this application is a JSON API that
        /// calls <c>AddControllers</c> - the attribute therefore resolves to a type that is not
        /// in the container and every protected request fails with a 500. Pulling the Razor view
        /// engine in to satisfy an attribute would be the wrong trade for an API that has no
        /// views.
        ///
        /// The second reason is the response. The attribute answers a bad token with a 400 and
        /// no body at all, and the frontend shows what it finds in <c>data.message</c>, so the
        /// visitor would be told "something went wrong" instead of "reload the page". Asking the
        /// service directly produces the same verdict with a body that says what happened.
        /// </remarks>
        private async Task<IActionResult?> RejectsCsrfAsync()
        {
            try
            {
                await _antiforgery.ValidateRequestAsync(HttpContext);
            }
            catch (AntiforgeryValidationException exception)
            {
                _logger.LogInformation(
                    exception,
                    "Refused a request to {Path} because its antiforgery token did not validate.",
                    HttpContext.Request.Path);

                return StatusCode(
                    StatusCodes.Status400BadRequest,
                    new MessageResponse
                    {
                        Code = InvalidCsrfCode,
                        Message = "The request could not be verified. Reload the page and try again."
                    });
            }

            return null;
        }

        /// <summary>
        /// Runs the reCAPTCHA check for an action. Anything other than an outright refusal lets
        /// the request continue, which is what makes an installation without a secret usable.
        /// </summary>
        private async Task<bool> PassesCaptchaAsync(
            string action,
            string clientAddress,
            CancellationToken cancellationToken)
        {
            var token = Request.Headers[CaptchaHeaderName].FirstOrDefault();

            var outcome = await _captchaValidator.ValidateAsync(
                token,
                action,
                clientAddress,
                cancellationToken);

            return outcome is not RecaptchaValidationOutcome.Failed;
        }

        private IActionResult CaptchaRefused()
            => StatusCode(
                StatusCodes.Status400BadRequest,
                new MessageResponse
                {
                    Code = InvalidCaptchaCode,
                    Message = "The request could not be verified as coming from a person. Please try again."
                });

        /// <summary>
        /// Refuses a client that has spent its window, and says for how long so that a caller can
        /// tell the visitor what to expect.
        /// </summary>
        private IActionResult Throttled(string code, TimeSpan window)
        {
            Response.Headers.RetryAfter = ((int)window.TotalSeconds).ToString(CultureInfo.InvariantCulture);

            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new MessageResponse
                {
                    Code = code,
                    Message = $"Too many attempts. Please try again in {Describe(window)}."
                });
        }

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

        /// <summary>
        /// A window in words, rounded to whole minutes so that a fifteen minute wait does not come
        /// out as "14.99 minutes".
        /// </summary>
        private static string Describe(TimeSpan window)
        {
            var minutes = (int)Math.Round(window.TotalMinutes, MidpointRounding.AwayFromZero);

            return minutes == 1 ? "1 minute" : $"{minutes} minutes";
        }
    }
}