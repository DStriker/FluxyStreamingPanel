using Fluxy.Core.Models.Users;

namespace Fluxy.Application.Services.Authentication
{
    /// <summary>
    /// Settings of token issuing and session handling. Bound from the <c>Auth</c> section.
    /// </summary>
    /// <remarks>
    /// The lifetimes here are the core of the design and they pull in opposite directions. The
    /// access token is short because it cannot be withdrawn, so the window during which a leaked
    /// one keeps working is exactly its remaining lifetime. The refresh token is long because it
    /// is stored hashed, is single use, and can be revoked outright - the properties a
    /// short-lived access token has none of. Five minutes and a week is what follows from that:
    /// the one token that cannot be killed lives minutes, and the one that can lives long
    /// enough that signing in once a week is a reasonable expectation.
    /// </remarks>
    public sealed class AuthenticationOptions
    {
        /// <summary>Configuration section this type is bound from.</summary>
        public const string SectionName = "Auth";

        /// <summary>
        /// Value the <c>iss</c> claim has to carry. It tells a token from this installation
        /// apart from a valid one minted for something else with the same key.
        /// </summary>
        public string Issuer { get; set; } = "fluxy";

        /// <summary>
        /// Value the <c>aud</c> claim has to carry. Together with the issuer this is what stops
        /// a token this server signed from being replayed at a different service.
        /// </summary>
        public string Audience { get; set; } = "fluxy-api";

        /// <summary>
        /// How long an access token is accepted. Deliberately minutes rather than hours: it
        /// cannot be withdrawn, so this is the exposure a leak gets.
        /// </summary>
        public TimeSpan AccessLifetime { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// How long a refresh chain stays open from its sign-in. Rolling rather than absolute,
        /// so an account in use keeps its session while an abandoned one lapses.
        /// </summary>
        public TimeSpan RefreshLifetime { get; set; } = TimeSpan.FromDays(7);

        /// <summary>
        /// Secret the access token is signed with, shared with nothing else.
        /// </summary>
        /// <remarks>
        /// Read from the gitignored <c>.env</c> file as <c>JWT_SIGNING_KEY</c> and never from
        /// <c>appsettings.json</c>, for the same reason the reCAPTCHA secret is not there: a key
        /// present in appsettings shadows the file, so even an empty placeholder would silently
        /// win over the real one. Rotating it invalidates every access token signed with it at
        /// once, which is the intended way to end every session on the installation.
        /// </remarks>
        public string? SigningKey { get; set; }

        /// <summary>
        /// Name of the cookie the access token travels in. Spelled with a prefix so it cannot
        /// collide with the application's own cookies.
        /// </summary>
        public const string AccessCookieName = "fluxy.at";

        /// <summary>
        /// Name of the cookie the refresh token travels in. It is never read by the browser, and
        /// it is a separate cookie from the access one so that the two can have different
        /// lifetimes and so that revoking the session does not have to be inferred from the
        /// access token being absent.
        /// </summary>
        public const string RefreshCookieName = "fluxy.rt";

        /// <summary>
        /// Page a browser is sent to when it holds no usable session. Every sign-in form lives
        /// under a different path, so this one is named rather than assumed - it is the client
        /// area, which is the only entry point a visitor is expected to have.
        /// </summary>
        public string ClientLoginPath { get; set; } = "login";

        /// <summary>
        /// Where each role lands after signing in. Three pages, one per role, none of which
        /// exists yet - the values are here so the paths are decided in one place rather than
        /// spelled into a response by hand.
        /// </summary>
        public string AdminLandingPath { get; set; } = "admin/dashboard";

        /// <summary>Where a reseller lands after signing in.</summary>
        public string ResellerLandingPath { get; set; } = "reseller/dashboard";

        /// <summary>Where a client lands after signing in.</summary>
        public string ClientLandingPath { get; set; } = "client/index";

        /// <summary>
        /// Landing path of a role, or the client login path for a value that is not a role.
        /// </summary>
        /// <param name="role">Role to look up.</param>
        public string LandingPathOf(UserRole role) => role switch
        {
            UserRole.Admin => AdminLandingPath,
            UserRole.Reseller => ResellerLandingPath,
            UserRole.Client => ClientLandingPath,
            _ => ClientLoginPath
        };
    }
}
