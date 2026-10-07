using System.Globalization;
using Fluxy.API.Configuration;
using Fluxy.API.Contracts;
using Fluxy.Core.Abstractions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Fluxy.API.Controllers
{
    /// <summary>
    /// The checks every endpoint of this API runs before it does anything else, and the
    /// refusals they produce.
    /// </summary>
    /// <remarks>
    /// Three guards sit in front of every endpoint an unauthenticated visitor can reach, and
    /// they are the same three everywhere: the antiforgery pair, the captcha, and the attempt
    /// limit. Only registration and sign-in need all of them, and only registration additionally
    /// needs a mail check, so those live in the derived controllers where they are used.
    ///
    /// They are here rather than copied because the order they run in is a security property
    /// rather than a matter of taste, and a copy is free to get it subtly wrong. The order is
    /// CSRF, then captcha, then limit, then the expensive work: a request with no valid
    /// antiforgery pair is not this application's request at all and is refused before it can
    /// cost anything; the captcha is a call to a third party and should not be made for a
    /// request that is already going to be refused; and the limit is what keeps a deliberate
    /// key derivation off an open endpoint. Each derived controller states the order for its
    /// own endpoint, and this class only supplies the pieces.
    ///
    /// It is an abstract controller rather than a filter or middleware on purpose. Each refusal
    /// is a specific status code and a specific code string, and the frontend branches on those
    /// strings; a filter would have to decide them centrally, which is exactly the coupling
    /// being avoided.
    /// </remarks>
    public abstract class AuthControllerBase : ControllerBase
    {
        /// <summary>Header the reCAPTCHA token arrives in.</summary>
        public const string CaptchaHeaderName = "X-Recaptcha-Token";

        /// <summary>Code reported when the antiforgery token is missing, stale or does not match.</summary>
        public const string InvalidCsrfCode = "csrf_invalid";

        /// <summary>Code reported when the reCAPTCHA check refuses a request.</summary>
        public const string InvalidCaptchaCode = "captcha_invalid";

        /// <summary>Address used as a throttle key when the connection has none.</summary>
        protected const string UnknownClient = "unknown";

        /// <summary>Counter that limits how often one client may try.</summary>
        protected readonly IAttemptThrottle Throttle;

        /// <summary>Service that builds and checks the CSRF token.</summary>
        protected readonly IAntiforgery Antiforgery;

        /// <summary>Live limits, so a configuration reload applies without a restart.</summary>
        protected readonly IOptionsMonitor<RateLimitOptions> RateLimits;

        /// <summary>Logger every derived controller reports through.</summary>
        protected readonly ILogger Logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthControllerBase"/> class.
        /// </summary>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="throttle">Counter that limits how often one client may try.</param>
        /// <param name="rateLimits">Live limits, so a configuration reload applies without a restart.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        protected AuthControllerBase(
            IAntiforgery antiforgery,
            IAttemptThrottle throttle,
            IOptionsMonitor<RateLimitOptions> rateLimits,
            ILogger logger)
        {
            Antiforgery = antiforgery;
            Throttle = throttle;
            RateLimits = rateLimits;
            Logger = logger;
        }

        /// <summary>
        /// Address the request came from, as this server saw it.
        /// </summary>
        /// <remarks>
        /// Nothing sits in front of the application yet, so this is the address of the socket
        /// and it is exactly the client. Put a proxy in front and this becomes the proxy's
        /// address, and every client would then share one window - which is the failure that
        /// shows up as "everybody is rate limited at once". Reading the forwarded headers is the
        /// fix, and it has to happen in the pipeline as early as possible, before anything reads
        /// the address. It is deliberately not done yet, because the address is also what the
        /// limit keys on and an unvalidated forwarded header is a way to mint unlimited windows.
        /// </remarks>
        protected string ClientAddress
            => HttpContext.Connection.RemoteIpAddress?.ToString() ?? UnknownClient;

        /// <summary>
        /// Checks the antiforgery token and, when it does not hold, builds the refusal.
        /// </summary>
        /// <returns>Null when the request may continue, otherwise the response to answer with.</returns>
        /// <remarks>
        /// The service is called directly rather than through
        /// <c>[ValidateAntiForgeryToken]</c>, for two reasons that hold for every endpoint in
        /// this file and are easy to get wrong again. That attribute resolves to an MVC filter
        /// that only <c>AddControllersWithViews</c> registers, and this application is a JSON API
        /// that calls <c>AddControllers</c> - the attribute therefore resolves to a type that is
        /// not in the container and every protected request fails with a 500. Pulling the Razor
        /// view engine in to satisfy an attribute would be the wrong trade for an API that has no
        /// views.
        ///
        /// The second reason is the response. The attribute answers a bad token with a 400 and
        /// no body at all, and the frontend shows what it finds in <c>data.message</c>, so the
        /// visitor would be told "something went wrong" instead of "reload the page". Asking the
        /// service directly produces the same verdict with a body that says what happened.
        /// </remarks>
        protected async Task<IActionResult?> RejectsCsrfAsync()
        {
            try
            {
                await Antiforgery.ValidateRequestAsync(HttpContext);
            }
            catch (AntiforgeryValidationException exception)
            {
                Logger.LogInformation(
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
        /// <param name="action">
        /// Action the token has to have been minted for. Fixed per endpoint and never taken from
        /// the request: a token minted for one form must not be usable at another.
        /// </param>
        /// <param name="clientAddress">Address to report to the verification service.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        protected async Task<bool> PassesCaptchaAsync(
            IRecaptchaValidator captchaValidator,
            string action,
            string clientAddress,
            CancellationToken cancellationToken)
        {
            var token = Request.Headers[CaptchaHeaderName].FirstOrDefault();

            var outcome = await captchaValidator.ValidateAsync(
                token,
                action,
                clientAddress,
                cancellationToken);

            return outcome is not RecaptchaValidationOutcome.Failed;
        }

        /// <summary>
        /// The refusal for a request the captcha would not vouch for.
        /// </summary>
        protected IActionResult CaptchaRefused()
            => StatusCode(
                StatusCodes.Status400BadRequest,
                new MessageResponse
                {
                    Code = InvalidCaptchaCode,
                    Message = "The request could not be verified as coming from a person. Please try again."
                });

        /// <summary>
        /// Marks the answer as one no intermediary may keep: <c>Cache-Control: no-store</c>.
        /// </summary>
        /// <remarks>
        /// For every read that answers about the caller — who the token belongs to, the
        /// profile, the visit history, the live sessions, the network the request arrived
        /// from — and for the antiforgery token itself. These bodies sit behind cookies a
        /// shared cache has no business matching on, and a cached "who am I" would hand one
        /// visitor's account to the next one through the same proxy. The writes are not
        /// marked: a POST or a DELETE is not cached in the first place, and a header
        /// promising something about a response nobody would store is noise on every one of
        /// them.
        /// </remarks>
        protected void NoStore()
            => Response.Headers.CacheControl = "no-store";

        /// <summary>
        /// Refuses a client that has spent its window, and says for how long so that a caller can
        /// tell the visitor what to expect.
        /// </summary>
        protected IActionResult Throttled(string code, TimeSpan window)
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
        /// A window in words, rounded to whole minutes so that a fifteen minute wait does not
        /// come out as "14.99 minutes".
        /// </summary>
        private static string Describe(TimeSpan window)
        {
            var minutes = (int)Math.Round(window.TotalMinutes, MidpointRounding.AwayFromZero);

            return minutes == 1 ? "1 minute" : $"{minutes} minutes";
        }
    }
}