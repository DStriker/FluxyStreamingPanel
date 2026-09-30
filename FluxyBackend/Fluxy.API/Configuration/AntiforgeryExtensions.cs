using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxy.API.Configuration
{
    /// <summary>
    /// Wiring of the cross site request forgery protection.
    /// </summary>
    public static class AntiforgeryExtensions
    {
        /// <summary>
        /// Request header a client sends its token in. It is what the frontend already sets, and
        /// a header is used instead of a form field because a JSON body has nowhere to put one.
        /// </summary>
        public const string HeaderName = "X-CSRF-Token";

        /// <summary>
        /// Cookie the token is also written to, spelled so that a browser can read it.
        /// </summary>
        /// <remarks>
        /// The framework's own cookie stays as it is. It holds the encrypted security token and
        /// is not readable by design; only the matching request token is, and a JavaScript client
        /// cannot ask the server for that on every request without making one. Writing it once
        /// into a readable cookie is what lets the frontend read it synchronously instead of
        /// awaiting a round trip before every form.
        /// </remarks>
        public const string TokenCookieName = "XSRF-TOKEN";

        /// <summary>
        /// Registers the antiforgery services.
        /// </summary>
        /// <param name="services">Service collection to register the services in.</param>
        /// <remarks>
        /// This registers the <see cref="IAntiforgery"/> service and its options. It does **not**
        /// make <c>[ValidateAntiForgeryToken]</c> work: that attribute resolves to an MVC filter
        /// which only <c>AddControllersWithViews</c> registers, and this project calls
        /// <c>AddControllers</c> because it serves JSON and has no views. The two endpoints that
        /// change something therefore call <c>IAntiforgery.ValidateRequestAsync</c> themselves,
        /// which also lets them answer a bad token with a body instead of a bare 400.
        ///
        /// The cookie is marked <see cref="SameSiteMode.Lax"/> rather than strict. Both stop the
        /// cookie from riding along on a cross site form post, which is the attack, but lax also
        /// survives a client that arrives from a link, and that costs nothing here.
        /// </remarks>
        public static IServiceCollection AddFluxyAntiforgery(this IServiceCollection services)
        {
            services.AddAntiforgery(options =>
            {
                options.HeaderName = HeaderName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });

            return services;
        }
    }
}