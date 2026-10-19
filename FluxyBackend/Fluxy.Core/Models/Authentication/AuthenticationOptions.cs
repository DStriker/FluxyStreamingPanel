using Fluxy.Core.Users.Enums;

namespace Fluxy.Core.Models.Authentication
{
    /// <summary>
    /// Settings of token issuing and session handling. Bound from the <c>Auth</c> section.
    /// </summary>
    /// <remarks>
    /// It lives in Core because both sides of the wire read it: the API layer binds and validates
    /// the section, and the services that issue, rotate and retire tokens resolve the same object
    /// for the cookie names, the landing paths and the lifetimes. One definition is what keeps a
    /// renamed cookie from being written by one side and looked for in vain by the other.
    ///
    /// The lifetimes here are the core of the design and they pull in opposite directions. The
    /// access token is short because it cannot be edited after it is signed: a leaked one keeps
    /// working for what is left of its life unless the session it belongs to is revoked, and a
    /// session marker can only be written while the store behind it is reachable. The refresh
    /// token is long because it is stored hashed, is single use, and can be revoked outright -
    /// the properties a short-lived access token has none of. Five minutes and a week is what
    /// follows from that: the token that cannot be killed lives minutes, and the one that can
    /// lives long enough that signing in once a week is a reasonable expectation.
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
        /// cannot be edited after signing, so this is the exposure a leaked token gets when its
        /// session marker could not be written - for example while redis is unreachable.
        /// </summary>
        public TimeSpan AccessLifetime { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// How long a refresh chain stays open from its sign-in. Rolling rather than absolute,
        /// so an account in use keeps its session while an abandoned one lapses.
        /// </summary>
        public TimeSpan RefreshLifetime { get; set; } = TimeSpan.FromDays(7);

        /// <summary>
        /// Tolerance added to both sides of a token's own lifetime, shared by validation and
        /// by the revocation marker so the two cannot disagree about when a token has died.
        /// </summary>
        /// <remarks>
        /// A token minted by one instance a moment ago can arrive before another one's clock
        /// has caught up, so validation accepts it for this long after <c>exp</c>. A revoked
        /// session has to be remembered for at least as long, otherwise the marker would
        /// expire while that same tolerance still let the token through.
        /// </remarks>
        public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

        /// <summary>
        /// How long the revocation marker of a session has to outlive every access token the
        /// session could still present.
        /// </summary>
        /// <remarks>
        /// Every access token of a session is minted with <see cref="AccessLifetime"/>, so the
        /// oldest one still in circulation has at most that much time left - plus the clock
        /// skew validation allows. The marker is kept for exactly that long, which is why it
        /// never needs a cleanup pass.
        /// </remarks>
        public TimeSpan SessionRevocationLifetime => AccessLifetime + ClockSkew;

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
