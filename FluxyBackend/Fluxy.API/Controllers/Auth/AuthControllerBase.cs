using Microsoft.AspNetCore.Antiforgery;

namespace Fluxy.API.Controllers.Auth
{
    /// <summary>
    /// The facts the controllers that run the guest checks share: the antiforgery pair and the
    /// no-store marker from <see cref="ApiControllerBase"/>, plus the address the guest guards
    /// key on.
    /// </summary>
    /// <remarks>
    /// The captcha and the attempt window used to live here as protected helpers. When the
    /// controllers were grouped by area, <c>SessionsController</c> inherited this class only
    /// because it started life inside <c>ProfileController</c>, and it had no use for either
    /// guard. They are now composed into the four controllers that run the guest checks as
    /// <see cref="Support.CaptchaGuard"/> and <see cref="Support.AttemptLimiter"/>; this base
    /// keeps the address both guards count by and the CSRF/no-store behaviour every area
    /// shares.
    /// </remarks>
    public abstract class AuthControllerBase : ApiControllerBase
    {
        /// <summary>Address used as a throttle key when the connection has none.</summary>
        protected const string UnknownClient = "unknown";

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthControllerBase"/> class.
        /// </summary>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        protected AuthControllerBase(IAntiforgery antiforgery, ILogger logger)
            : base(antiforgery, logger)
        {
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
    }
}
