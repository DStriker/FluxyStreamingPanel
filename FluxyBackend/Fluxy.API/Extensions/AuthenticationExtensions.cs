using Fluxy.API.Middleware;
using Fluxy.API.Security;
using Fluxy.Core.Authentication.Interfaces;
using Fluxy.Core.Authentication.Options;
using Fluxy.Core.Authentication.Utils;
using Fluxy.Core.Users.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fluxy.API.Extensions
{
    /// <summary>
    /// Wiring of authentication and authorization: the scheme that reads a token, the policies
    /// an endpoint declares, and the rules that decide what a valid token still grants.
    /// </summary>
    /// <remarks>
    /// The two halves are kept apart on purpose, because they answer different questions and
    /// fail differently. Authentication asks "is this token genuine, unexpired and not withdrawn"
    /// and is a pure check of a signature and a lifetime - it can be answered from the token
    /// alone, which is what makes it cheap. Authorization asks "may this account do this now",
    /// and that cannot be answered from the token at all: a token's role claim is a statement
    /// about the moment it was signed, so a demoted, blocked or deleted account would keep
    /// presenting its old role until it expired. Every authorized request therefore reads the
    /// account back, and <see cref="IAccessChecker"/> is what does it.
    /// </remarks>
    public static class AuthenticationExtensions
    {
        /// <summary>
        /// Name of the policy that accepts an operator and nothing else.
        /// </summary>
        public const string AdminPolicy = "AdminOnly";

        /// <summary>Name of the policy that accepts a reseller and nothing else.</summary>
        public const string ResellerPolicy = "ResellerOnly";

        /// <summary>Name of the policy that accepts a client and nothing else.</summary>
        public const string ClientPolicy = "ClientOnly";

        /// <summary>
        /// Policy that accepts an operator who may read the accounts of at least one role.
        /// </summary>
        /// <remarks>
        /// The <i>coarse</i> gate, and deliberately coarse: an endpoint behind it knows that
        /// the caller may read some of the installation's accounts but not which, because the
        /// target of a request is only known once the request is being handled. The decision
        /// about a specific account is taken inside the action, against
        /// <c>UserPermissionCatalog.ViewFor</c>. Keeping this policy is what preserves the
        /// property the controllers were built on - an action that forgot to narrow the gate
        /// is readable by too many administrators rather than by all of them.
        /// </remarks>
        public const string ViewAnyUsersPolicy = "Permission.ViewAnyUsers";

        /// <summary>
        /// Policy that accepts an operator who may change the accounts of at least one role.
        /// The coarse half of the same two-layer gate; see <see cref="ViewAnyUsersPolicy"/>.
        /// </summary>
        public const string EditAnyUsersPolicy = "Permission.EditAnyUsers";

        /// <summary>Policy that accepts an operator who may read the groups list.</summary>
        public const string ViewUserGroupsPolicy = "Permission.ViewUserGroups";

        /// <summary>Policy that accepts an operator who may change the groups.</summary>
        public const string EditUserGroupsPolicy = "Permission.EditUserGroups";

        /// <summary>Policy that accepts an operator who may read the configured video streams.</summary>
        public const string ViewStreamsPolicy = "Permission.ViewStreams";

        /// <summary>Policy that accepts an operator who may change the configured video streams.</summary>
        public const string EditStreamsPolicy = "Permission.EditStreams";

        /// <summary>Policy that accepts an operator who may read the configured audio streams.</summary>
        public const string ViewAudioStreamsPolicy = "Permission.ViewAudioStreams";

        /// <summary>Policy that accepts an operator who may change the configured audio streams.</summary>
        public const string EditAudioStreamsPolicy = "Permission.EditAudioStreams";

        /// <summary>Policy that accepts an operator who may read the on-demand catalogue.</summary>
        public const string ViewVideoOnDemandPolicy = "Permission.ViewVideoOnDemand";

        /// <summary>Policy that accepts an operator who may change the on-demand catalogue.</summary>
        public const string EditVideoOnDemandPolicy = "Permission.EditVideoOnDemand";

        /// <summary>Policy that accepts an operator who may read the series catalogue.</summary>
        public const string ViewSeriesPolicy = "Permission.ViewSeries";

        /// <summary>Policy that accepts an operator who may change the series catalogue.</summary>
        public const string EditSeriesPolicy = "Permission.EditSeries";

        /// <summary>Policy that accepts an operator who may read the programme guide sources.</summary>
        public const string ViewEpgSourcesPolicy = "Permission.ViewEpgSources";

        /// <summary>Policy that accepts an operator who may change the programme guide sources.</summary>
        public const string EditEpgSourcesPolicy = "Permission.EditEpgSources";

        /// <summary>
        /// Registers the JWT scheme and the authorization policies.
        /// </summary>
        /// <param name="services">Service collection to register into.</param>
        /// <param name="configuration">Configuration the <c>Auth</c> section is bound from.</param>
        /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
        /// <remarks>
        /// The signing key is read here, once, and turned into the parameters the handler
        /// validates with. That is deliberate: a key the handler had to look up per request
        /// would be a key that could be absent at exactly the moment a token arrives, and a
        /// misconfigured installation should fail at startup instead of answering every request
        /// as unauthorized while looking healthy. The lifetimes and the cookie names are bound
        /// and validated here as well, because this is the method that owns every other decision
        /// about them.
        /// </remarks>
        public static IServiceCollection AddFluxyAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Resolved lazily by the scheme and the token service, so unlike the signing key
            // below they cannot be read into anything the code already holds - the options
            // object is validated while the host starts instead, which is what turns a zero or
            // an inverted lifetime into a startup error rather than into a stream of 401s.
            services.AddOptions<AuthenticationOptions>()
                .Bind(configuration.GetSection(AuthenticationOptions.SectionName))
                .Validate(
                    options => options.AccessLifetime > TimeSpan.Zero,
                    $"'{AuthenticationOptions.SectionName}' needs an AccessLifetime longer than " +
                    "zero, otherwise no request could ever be authorized.")
                .Validate(
                    options => options.RefreshLifetime >= options.AccessLifetime,
                    $"'{AuthenticationOptions.SectionName}' needs a RefreshLifetime at least as " +
                    "long as the AccessLifetime, otherwise a session would expire while its " +
                    "access token was still valid and no token could ever be exchanged.")
                // Deliberately not validated here. The signing key is checked once, eagerly, by
                // the code below, because it has to be turned into bytes before the scheme can
                // exist at all - it cannot be a lazy validation on an options object nothing
                // resolves until the first authorized request.
                .ValidateOnStart();

            var options = configuration
                .GetSection(AuthenticationOptions.SectionName)
                .Get<AuthenticationOptions>() ?? new AuthenticationOptions();

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(jwt =>
                {
                    jwt.MapInboundClaims = false;
                    jwt.TokenValidationParameters = BuildValidationParameters(options);

                    // A browser holds its token in a cookie, so the Authorization header is
                    // usually absent. The header is still read first, which is what lets a
                    // non-browser client - curl, a native app, another server - send the same
                    // token with no cookies and no knowledge of this application.
                    jwt.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = ReadTokenFromCookie,
                        OnTokenValidated = RefuseRevokedSession
                    };
                });

            services.AddAuthorizationBuilder()
                .AddPolicy(AdminPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new RoleRequirement(Fluxy.Core.Users.Enums.UserRole.Admin)))
                .AddPolicy(ResellerPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new RoleRequirement(Fluxy.Core.Users.Enums.UserRole.Reseller)))
                .AddPolicy(ClientPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new RoleRequirement(Fluxy.Core.Users.Enums.UserRole.Client)))

                // The permission policies. Each one carries *both* requirements, and that is
                // the whole design: the role stays the base gate, so an endpoint behind one of
                // these still refuses an account of the wrong level even if the permission it
                // was granted somehow says otherwise - and a controller that adds a permission
                // policy on top of the class's role policy gets the two merged into a single
                // evaluation, which is what keeps "two checks" from meaning two answers.
                //
                // The role named here is the role that owns the permissions in
                // <c>UserPermissionCatalog</c>, which is why all of them say Admin: a
                // permission only exists for the role that owns it, so a policy for a
                // permission of another role names that role instead.
                //
                // The two account policies demand *any* of the three <c>view*</c> (or
                // <c>edit*</c>) keys rather than one, because one endpoint serves all three
                // roles and which role it is about is not known until the request is being
                // handled. What the policy settles is that this caller may read - or write -
                // some accounts of the installation at all; which ones is decided inside the
                // action, against <c>UserPermissionCatalog.ViewFor</c>/<c>EditFor</c>.
                .AddPolicy(ViewAnyUsersPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new RoleRequirement(Fluxy.Core.Users.Enums.UserRole.Admin),
                        new PermissionRequirement(
                            Fluxy.Core.Users.Utils.UserPermissionCatalog.ViewingAccounts
                                .ToArray())))
                .AddPolicy(EditAnyUsersPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new RoleRequirement(Fluxy.Core.Users.Enums.UserRole.Admin),
                        new PermissionRequirement(
                            Fluxy.Core.Users.Utils.UserPermissionCatalog.EditingAccounts
                                .ToArray())))
                .AddPolicy(ViewUserGroupsPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new RoleRequirement(Fluxy.Core.Users.Enums.UserRole.Admin),
                        new PermissionRequirement(Fluxy.Core.Users.Enums.UserPermission.ViewUserGroups)))
                .AddPolicy(EditUserGroupsPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new RoleRequirement(Fluxy.Core.Users.Enums.UserRole.Admin),
                        new PermissionRequirement(Fluxy.Core.Users.Enums.UserPermission.EditUserGroups)))

                // The stream policies are single-key rather than "any of", the way the group
                // ones are: there is one list per medium and one screen that draws it, so
                // unlike the accounts there is no second axis to be coarse about. Video and
                // audio are separate pairs because they are separate screens and an operator
                // may run one without the other. The role requirement is still on top of the
                // permission for the same reason it is on the group policies - a permission
                // only means anything once the role gate ahead of it has already let the
                // caller into the admin area.
                .AddPolicy(ViewStreamsPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new RoleRequirement(Fluxy.Core.Users.Enums.UserRole.Admin),
                        new PermissionRequirement(Fluxy.Core.Users.Enums.UserPermission.ViewStreams)))
                .AddPolicy(EditStreamsPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new RoleRequirement(Fluxy.Core.Users.Enums.UserRole.Admin),
                        new PermissionRequirement(Fluxy.Core.Users.Enums.UserPermission.EditStreams)))
                .AddPolicy(ViewAudioStreamsPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new RoleRequirement(Fluxy.Core.Users.Enums.UserRole.Admin),
                        new PermissionRequirement(Fluxy.Core.Users.Enums.UserPermission.ViewAudioStreams)))
                .AddPolicy(EditAudioStreamsPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new RoleRequirement(Fluxy.Core.Users.Enums.UserRole.Admin),
                        new PermissionRequirement(Fluxy.Core.Users.Enums.UserPermission.EditAudioStreams)))
                .AddPolicy(ViewVideoOnDemandPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new RoleRequirement(Fluxy.Core.Users.Enums.UserRole.Admin),
                        new PermissionRequirement(Fluxy.Core.Users.Enums.UserPermission.ViewVideoOnDemand)))
                .AddPolicy(EditVideoOnDemandPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new RoleRequirement(Fluxy.Core.Users.Enums.UserRole.Admin),
                        new PermissionRequirement(Fluxy.Core.Users.Enums.UserPermission.EditVideoOnDemand)))
                .AddPolicy(ViewSeriesPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new RoleRequirement(Fluxy.Core.Users.Enums.UserRole.Admin),
                        new PermissionRequirement(Fluxy.Core.Users.Enums.UserPermission.ViewSeries)))
                .AddPolicy(EditSeriesPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new RoleRequirement(Fluxy.Core.Users.Enums.UserRole.Admin),
                        new PermissionRequirement(Fluxy.Core.Users.Enums.UserPermission.EditSeries)))
                .AddPolicy(ViewEpgSourcesPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new RoleRequirement(Fluxy.Core.Users.Enums.UserRole.Admin),
                        new PermissionRequirement(Fluxy.Core.Users.Enums.UserPermission.ViewEpgSources)))
                .AddPolicy(EditEpgSourcesPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new RoleRequirement(Fluxy.Core.Users.Enums.UserRole.Admin),
                        new PermissionRequirement(Fluxy.Core.Users.Enums.UserPermission.EditEpgSources)));

            // Authorization handlers are resolved per request, and they depend on a scoped access checker
            // (which reads the DbContext). Registering as Scoped avoids the singleton->scoped
            // validation failure, and there is no state worth keeping for the lifetime of the app.
            services.AddScoped<IAuthorizationHandler, RoleRequirementHandler>();
            services.AddScoped<IAuthorizationHandler, PermissionRequirementHandler>();

            // The shared read of the account, scoped so that one request's answer is never
            // another request's - and so the result handler can ask the same question again
            // without paying for it a second time.
            services.AddScoped<RequestStandingReader>();

            // Every authorization outcome comes back as the documented body rather than as the
            // framework's bare 401 with a WWW-Authenticate header, which a browser would
            // otherwise turn into a login prompt of its own making.
            services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationResultHandler>();

            return services;
        }

        /// <summary>
        /// The rules a token has to satisfy, and the two things that are deliberately not
        /// switched on.
        /// </summary>
        /// <remarks>
        /// Issuer and audience are both validated, which together are what stop a token this
        /// server signed from being replayed somewhere else that happens to trust the same key.
        /// The lifetime is validated with a small clock skew, because a token minted by another
        /// instance a moment ago can arrive before this one's clock has caught up - and the skew
        /// is bounded so it cannot extend a token's life by anything meaningful.
        ///
        /// <see cref="TokenValidationParameters.ValidateAudience"/> and
        /// <see cref="TokenValidationParameters.ValidateIssuer"/> are left at their defaults,
        /// which are both true. The alternative - requiring claims that are not always present -
        /// is a way of finding out in production which token was minted by which build.
        /// </remarks>
        private static TokenValidationParameters BuildValidationParameters(AuthenticationOptions options)
        {
            // The very same key material the token service signs with, built by the very same
            // method. Anything else - a second copy of the key, a different encoding of the same
            // text - would validate nothing at all, and would fail as a stream of 401s that look
            // like a clock problem rather than like the configuration error it is.
            var key = new SymmetricSecurityKey(TokenSigningKey.Materialize(options.SigningKey));

            return new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidIssuer = options.Issuer,
                ValidAudience = options.Audience,
                ValidateLifetime = true,
                ClockSkew = AuthenticationOptions.ClockSkew,

                // The token names its own issuer and audience, so both are checked against the
                // configured values rather than against the claim. The signature is verified
                // against the configured key, and these two narrow what a key holder can mint
                // that this particular service will still accept.
                ValidateIssuer = true,
                ValidateAudience = true,

                NameClaimType = TokenClaimTypes.Name,
                RoleClaimType = TokenClaimTypes.Role
            };
        }

        /// <summary>
        /// Picks the token out of the access cookie when the caller sent no header.
        /// </summary>
        /// <param name="context">Context of the request being handled.</param>
        /// <returns>A task that completes once the token has been placed on the context.</returns>
        /// <remarks>
        /// The cookie is only read when the request arrived over a scheme that would have
        /// allowed it, and the header always wins. Reading it unconditionally would mean a
        /// token in a cookie that the browser withheld on purpose - a cross-site request - was
        /// picked up anyway, which is the one case a cookie is not meant to cover.
        /// </remarks>
        private static Task ReadTokenFromCookie(MessageReceivedContext context)
        {
            // The header always wins, so a caller that sends one is never overridden by a cookie
            // that happens to be on the request. The cookie is only consulted when the header is
            // absent, which is the normal state for a browser.
            if (string.IsNullOrEmpty(context.Token)
                && context.Request.Cookies.TryGetValue(
                    AuthenticationOptions.AccessCookieName, out var token)
                && !string.IsNullOrEmpty(token))
            {
                context.Token = token;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Refuses an access token whose session was withdrawn while the token was still
        /// inside its own lifetime.
        /// </summary>
        /// <remarks>
        /// This runs after the signature and the lifetime have been checked, so the only thing
        /// left to decide is whether the token is still one this server chose to honour. A
        /// signature being valid and a session being live are different claims: the first says
        /// the token was issued here, the second says the session it belongs to has not been
        /// ended since.
        ///
        /// A token that has been signed cannot be edited to say "no longer valid" - that is the
        /// property that makes it checkable without a database - and there is no claim a logout
        /// could flip. The session identifier is the part that can be named afterwards, one
        /// value covering every access token the session ever held, which is why the token
        /// carries it and why ending a session records it for exactly as long as the last
        /// possible token of that session would otherwise have worked.
        ///
        /// One store read per request that presents a token. It is the price of "sign out" being
        /// immediate rather than a request that takes effect in up to five minutes, and it is a
        /// single keyed read against a value that expires with the last token it refers to.
        /// </remarks>
        private static async Task RefuseRevokedSession(TokenValidatedContext context)
        {
            var sessionValue = context.Principal?.FindFirst(TokenClaimTypes.Session)?.Value;

            if (!Guid.TryParse(sessionValue, out var sessionId) || sessionId == Guid.Empty)
            {
                // A token with no usable session identifier cannot be checked, and every token
                // this server mints carries one. Refusing it is the safe reading: an
                // unidentified token is either not ours or minted by a build that predates the
                // claim, and neither is something to honour silently.
                context.Fail("The access token carries no valid session identifier, so it " +
                    "cannot be checked against revoked sessions.");

                return;
            }

            var revocations = context.HttpContext.RequestServices
                .GetRequiredService<ITokenRevocationStore>();

            if (await revocations.IsSessionBlockedAsync(sessionId, context.HttpContext.RequestAborted))
            {
                context.Fail("The session this access token belongs to was revoked.");
            }
        }
    }
}
