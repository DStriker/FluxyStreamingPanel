using Fluxy.Application.Services.Authentication;
using Fluxy.Core.Models.Authentication;
using Fluxy.Core.Models.Users;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts
{
    /// <summary>
    /// Turns what authentication reported into what a client is told: an HTTP status, a
    /// machine readable code, an English fallback, and the page the visitor belongs on.
    /// </summary>
    /// <remarks>
    /// The table lives in the transport layer and nowhere else, for the reason the registration
    /// table does: the service reports <see cref="AuthenticationStatus"/> and never learns that
    /// one answer is a 200 and another a 401. That separation is what lets the same outcome be
    /// served over a different transport later, or a status changed without a rule being touched.
    ///
    /// The set of codes is deliberately narrower than the set of things that can go wrong. A
    /// wrong password, an unknown name, an unconfirmed account, a blocked account and an account
    /// of the wrong role all produce
    /// <see cref="AuthenticationStatus.InvalidCredentials"/>, and therefore one code. A wider
    /// set would tell a caller which accounts exist and which roles they hold, which is exactly
    /// what a sign-in form should not be able to reveal.
    /// </remarks>
    internal static class AuthenticationResponses
    {
        /// <summary>Code reported when the credentials were good and a session was opened.</summary>
        public const string AuthenticatedCode = "authenticated";

        /// <summary>
        /// Code reported for every refused sign-in, whatever the reason. See the remarks on the
        /// class.
        /// </summary>
        public const string InvalidCredentialsCode = "invalid_credentials";

        /// <summary>Code reported when a client spent its sign-in attempts for this window.</summary>
        public const string LoginThrottledCode = "login_rate_limited";

        /// <summary>Code reported when a client spent its refreshes for this window.</summary>
        public const string RefreshThrottledCode = "refresh_rate_limited";

        /// <summary>
        /// Code reported when a refresh token was already spent, or the session it belonged to is
        /// gone. The visitor is sent to the sign-in page.
        /// </summary>
        public const string SessionExpiredCode = "session_expired";

        /// <summary>
        /// Code reported when a request needs a session and arrived without a usable one. Kept
        /// distinct from <see cref="SessionExpiredCode"/> so a client can tell "you were never
        /// signed in" from "your session ended", which are different things to say to somebody.
        /// </summary>
        public const string AuthenticationRequiredCode = "auth_required";

        /// <summary>
        /// Code reported when the session is genuine but its account may not have this area -
        /// the role in the token no longer matches the account, or does not match the endpoint.
        /// </summary>
        public const string RoleChangedCode = "auth_role_changed";

        /// <summary>Code reported after a session was ended. Not an error.</summary>
        public const string SignedOutCode = "signed_out";

        /// <summary>Answer describing <paramref name="status"/>.</summary>
        /// <param name="status">Outcome reported by the service.</param>
        /// <param name="role">
        /// Role the visitor ended up with, which decides the landing path on success. Ignored for
        /// every outcome that has no destination.
        /// </param>
        /// <param name="settings">Settings the landing paths are read from.</param>
        public static MessageResponse Describe(
            AuthenticationStatus status,
            UserRole? role = null,
            AuthenticationOptions? settings = null)
        {
            var mapping = Map(status);

            return new MessageResponse
            {
                Code = mapping.Code,
                Message = mapping.Message,
                Redirect = mapping.SendToLanding && role is { } landed && settings is not null
                    ? "/" + settings.LandingPathOf(landed).TrimStart('/')
                    : null
            };
        }

        /// <summary>HTTP status to answer <paramref name="status"/> with.</summary>
        /// <param name="status">Outcome reported by the service.</param>
        public static int StatusCodeOf(AuthenticationStatus status) => Map(status).StatusCode;

        /// <summary>
        /// The one place each outcome becomes a status, a code and a sentence.
        /// </summary>
        /// <remarks>
        /// A refusal carries no destination. Sending somebody somewhere on the strength of a
        /// failed attempt moves the person who mistyped a password away from the form that would
        /// fix it, and a redirect is not how an error is delivered anyway - this API answers in
        /// JSON, and a <c>fetch</c> that follows a redirect ends up holding a page it cannot
        /// parse.
        /// </remarks>
        private static Mapping Map(AuthenticationStatus status) => status switch
        {
            AuthenticationStatus.Authenticated => new(
                StatusCodes.Status200OK,
                AuthenticatedCode,
                "Signed in.",
                SendToLanding: true),

            // One code, one sentence, one status. The wording says only that the pair does not
            // work, which is all the truth a caller is entitled to, and says nothing about which
            // half of it was wrong or whether the account exists at all.
            AuthenticationStatus.InvalidCredentials => new(
                StatusCodes.Status401Unauthorized,
                InvalidCredentialsCode,
                "The username or password is not correct.",
                SendToLanding: false),

            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

        /// <summary>The four values one outcome is rendered into.</summary>
        private readonly record struct Mapping(
            int StatusCode,
            string Code,
            string Message,
            bool SendToLanding);
    }
}