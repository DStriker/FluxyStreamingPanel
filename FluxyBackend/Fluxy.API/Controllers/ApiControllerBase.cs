using Fluxy.API.Contracts.Common;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers
{
    /// <summary>
    /// The HTTP guards every controller of this API shares: the antiforgery pair and the
    /// no-store marker for answers about the caller.
    /// </summary>
    /// <remarks>
    /// Split out of <c>AuthControllerBase</c> when the controllers were grouped into the
    /// <c>Auth</c>, <c>Admin</c> and <c>Content</c> areas. The administrative and content
    /// endpoints carry a session and write state, so every one of them needs the antiforgery
    /// check, and their reads answer about the installation - so they need <c>NoStore</c> as
    /// well. What they do not have is a captcha or an attempt window: those guard an anonymous
    /// caller and an expensive key derivation, and everything in those areas is behind a
    /// signed-in administrator already. Inheriting the auth base would have handed them a
    /// throttle they never spend and made three areas depend on the fourth.
    ///
    /// The order the checks run in is a security property rather than a matter of taste: the
    /// antiforgery pair is checked first, before any body is read, because a request with no
    /// valid pair is not this application's request at all. Each derived controller states the
    /// rest of its own order.
    /// </remarks>
    public abstract class ApiControllerBase : ControllerBase
    {
        /// <summary>Code reported when the antiforgery token is missing, stale or does not match.</summary>
        public const string InvalidCsrfCode = "csrf_invalid";

        /// <summary>Service that builds and checks the CSRF token.</summary>
        protected readonly IAntiforgery Antiforgery;

        /// <summary>Logger every derived controller reports through.</summary>
        protected readonly ILogger Logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiControllerBase"/> class.
        /// </summary>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        protected ApiControllerBase(IAntiforgery antiforgery, ILogger logger)
        {
            Antiforgery = antiforgery;
            Logger = logger;
        }

        /// <summary>
        /// Checks the antiforgery token and, when it does not hold, builds the refusal.
        /// </summary>
        /// <returns>Null when the request may continue, otherwise the response to answer with.</returns>
        /// <remarks>
        /// The service is called directly rather than through
        /// <c>[ValidateAntiForgeryToken]</c>, for two reasons that hold for every endpoint in
        /// this API and are easy to get wrong again. That attribute resolves to an MVC filter
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
        /// Marks the answer as one no intermediary may keep: <c>Cache-Control: no-store</c>.
        /// </summary>
        /// <remarks>
        /// For every read that answers about the caller — who the token belongs to, the
        /// profile, the visit history, the live sessions, the accounts, the groups, the content
        /// structure, the network the request arrived from — and for the antiforgery token
        /// itself. These bodies sit behind cookies a shared cache has no business matching on,
        /// and a cached "who am I" would hand one visitor's account to the next one through the
        /// same proxy. The writes are not marked: a POST or a DELETE is not cached in the first
        /// place, and a header promising something about a response nobody would store is noise
        /// on every one of them.
        /// </remarks>
        protected void NoStore()
            => Response.Headers.CacheControl = "no-store";
    }
}
