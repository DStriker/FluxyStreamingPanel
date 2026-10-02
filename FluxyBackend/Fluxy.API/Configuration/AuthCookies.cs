using Fluxy.Application.Services.Authentication;
using Fluxy.Core.Models.Authentication;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Configuration
{
    /// <summary>
    /// Writes and clears the two cookies a session lives in.
    /// </summary>
    /// <remarks>
    /// Both the sign-in endpoints and the registration confirmation need to end up with a
    /// session, and they must produce identical cookies - a confirmation that wrote the token
    /// under different flags than a sign-in would leave a browser with a session the next
    /// request cannot read. So the writing is here, once, rather than in each controller.
    ///
    /// The reason tokens travel in cookies at all is that a browser cannot be trusted to keep
    /// them out of reach of scripts. A token in a JSON body, or in <c>localStorage</c>, is
    /// readable by every script on the origin - and injected script is a script on the origin -
    /// so a single cross-site scripting flaw anywhere in the frontend hands over the session. An
    /// <c>HttpOnly</c> cookie is not offered to script at all, which closes that whole class of
    /// attack rather than the particular instance of it.
    ///
    /// No token is ever returned in the response body. That is the same decision seen from the
    /// other side: a body the frontend receives is a body any script on the page has already had.
    /// A client that is not a browser - a native app, a server-to-server caller, curl - sends the
    /// token back in an <c>Authorization: Bearer</c> header instead, and the scheme reads that
    /// first.
    /// </remarks>
    public static class AuthCookies
    {
        /// <summary>
        /// Puts a freshly issued pair into the response as two cookies.
        /// </summary>
        /// <param name="context">Response the cookies are written to.</param>
        /// <param name="tokens">The pair to write.</param>
        public static void Write(HttpContext context, IssuedTokens tokens)
        {
            var options = BuildOptions(context);

            context.Response.Cookies.Append(
                AuthenticationOptions.AccessCookieName,
                tokens.AccessToken,
                BuildFor(tokens.AccessTokenExpiresAt, options));

            context.Response.Cookies.Append(
                AuthenticationOptions.RefreshCookieName,
                tokens.RefreshToken,
                BuildFor(tokens.RefreshTokenExpiresAt, options));

            DropCsrfCopy(context);
        }

        /// <summary>
        /// Removes both cookies, so the browser stops sending them.
        /// </summary>
        /// <param name="context">Response the cookies are cleared on.</param>
        /// <remarks>
        /// Deleted rather than set to an empty value, and with the same path and flags that were
        /// used to write them. A deletion whose attributes do not match the original is a
        /// different cookie as far as the browser is concerned, so it deletes nothing and the
        /// token keeps being sent.
        /// </remarks>
        public static void Clear(HttpContext context)
        {
            var options = BuildOptions(context);

            context.Response.Cookies.Delete(
                AuthenticationOptions.AccessCookieName,
                options);

            context.Response.Cookies.Delete(
                AuthenticationOptions.RefreshCookieName,
                options);

            DropCsrfCopy(context);
        }

        /// <summary>
        /// Drops the readable <c>XSRF-TOKEN</c> copy whenever the session cookies change.
        /// </summary>
        /// <remarks>
        /// The antiforgery service binds a token to the identity that was current when it was
        /// minted: a token issued while anonymous stops validating the moment the request
        /// carries a signed-in user, and the refusal is
        /// <c>"the token was meant for a different claims-based user"</c>. So the copy in the
        /// cookie is only good for the side of a sign-in or sign-out it was minted on, and
        /// leaving it behind would hand the next request a token that cannot possibly work -
        /// a stale copy is worse than none, because a missing one is fetched and a stale one
        /// is read.
        ///
        /// The client refetches before every write anyway; this is the other half of the same
        /// guarantee, so that even a client which reads the cookie synchronously cannot end up
        /// with the wrong side's token.
        /// </remarks>
        private static void DropCsrfCopy(HttpContext context)
        {
            context.Response.Cookies.Delete(
                AntiforgeryExtensions.TokenCookieName,
                new CookieOptions
                {
                    // Mirrors how the copy is written in `GetCsrfToken`: readable, and the
                    // same path and flags, or the deletion would address a different cookie.
                    HttpOnly = false,
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Path = "/"
                });
        }

        private static CookieOptions BuildOptions(HttpContext context) => new()
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            // Marked essential so the cookie survives a browser's "block third party cookies"
            // setting. It is not a tracking cookie, it is the session: withholding it would
            // sign every visitor out on any browser that applies that setting to a first-party
            // request.
            IsEssential = true
        };

        private static CookieOptions BuildFor(DateTimeOffset expires, CookieOptions template)
            => new()
            {
                HttpOnly = template.HttpOnly,
                Secure = template.Secure,
                SameSite = template.SameSite,
                Path = template.Path,
                IsEssential = template.IsEssential,
                Expires = expires
            };
    }
}