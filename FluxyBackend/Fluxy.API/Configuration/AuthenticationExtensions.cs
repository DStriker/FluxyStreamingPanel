using Fluxy.Application.Services.Authentication;
using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fluxy.API.Configuration
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
        /// as unauthorized while looking healthy.
        /// </remarks>
        public static IServiceCollection AddFluxyAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
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
                        OnTokenValidated = RefuseWithdrawnToken
                    };
                });

            services.AddAuthorizationBuilder()
                .AddPolicy(AdminPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new RoleRequirement(Fluxy.Core.Models.Users.UserRole.Admin)))
                .AddPolicy(ResellerPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new RoleRequirement(Fluxy.Core.Models.Users.UserRole.Reseller)))
                .AddPolicy(ClientPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new RoleRequirement(Fluxy.Core.Models.Users.UserRole.Client)));

            // Authorization handlers are resolved per request, and they depend on a scoped access checker
            // (which reads the DbContext). Registering as Scoped avoids the singleton->scoped
            // validation failure, and there is no state worth keeping for the lifetime of the app.
            services.AddScoped<IAuthorizationHandler, RoleRequirementHandler>();

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
                ClockSkew = TimeSpan.FromSeconds(30),

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
        /// Refuses a token that was withdrawn while it was still inside its own lifetime.
        /// </summary>
        /// <remarks>
        /// This runs after the signature and the lifetime have been checked, so the only thing
        /// left to decide is whether the token is still one this server chose to honour. A
        /// signature being valid and a token being live are different claims: the first says the
        /// token was issued here, the second says it has not been withdrawn since.
        ///
        /// A token that has been signed cannot be edited to say "no longer valid" - that is the
        /// property that makes it checkable without a database - and there is no claim a logout
        /// could flip. The identifier is the one part that can be named afterwards, which is why
        /// the token carries it and why a sign-out records it for exactly as long as the token
        /// would otherwise have worked.
        ///
        /// One store read per request that presents a token. It is the price of "sign out" being
        /// immediate rather than a request that takes effect in up to five minutes, and it is a
        /// single keyed read against a value that expires with the token it names.
        /// </remarks>
        private static async Task RefuseWithdrawnToken(TokenValidatedContext context)
        {
            var tokenId = context.Principal?.FindFirst(TokenClaimTypes.TokenId)?.Value;

            if (string.IsNullOrEmpty(tokenId))
            {
                // A token with no identifier cannot be withdrawn, and every token this server
                // mints carries one. Refusing it is the safe reading: an unidentified token is
                // either not ours or minted by a build that predates the claim, and neither is
                // something to honour silently.
                context.Fail("The access token carries no identifier, so it cannot be checked " +
                    "against withdrawn tokens.");

                return;
            }

            var revocations = context.HttpContext.RequestServices
                .GetRequiredService<ITokenRevocationStore>();

            if (await revocations.IsBlockedAsync(tokenId, context.HttpContext.RequestAborted))
            {
                context.Fail("The access token was withdrawn.");
            }
        }
    }

    /// <summary>
    /// Demands that the account behind a token holds one exact role.
    /// </summary>
    /// <remarks>
    /// Exact, not <c>AtLeast</c>, and the reason is that this is an entrance rather than a
    /// permission. The role enum is ordered precisely so that a permission check can be
    /// <c>role &gt;= required</c> - a reseller panel may accept an operator, because an operator
    /// holds every right a reseller has. But each of the three sign-in forms is a door to one
    /// area, and an operator arriving through the reseller door is not a lesser version of the
    /// same visitor: they are a different audience arriving at the wrong address, and the form
    /// that let them in is a form for someone else. Each form has its own endpoint and its own
    /// policy, and this requirement is where the two meet.
    /// </remarks>
    public sealed class RoleRequirement : IAuthorizationRequirement
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RoleRequirement"/> class.
        /// </summary>
        /// <param name="role">Role the endpoint accepts, and only that role.</param>
        public RoleRequirement(Fluxy.Core.Models.Users.UserRole role)
        {
            Role = role;
        }

        /// <summary>Role the endpoint accepts.</summary>
        public Fluxy.Core.Models.Users.UserRole Role { get; }
    }

    /// <summary>
    /// Decides a <see cref="RoleRequirement"/> against the account's current row, not against
    /// the token.
    /// </summary>
    /// <remarks>
    /// Two things have to agree before a request proceeds: the role the token was signed with,
    /// and the role the account holds now. A mismatch means the account changed after the token
    /// was issued, and there is no safe reading of that - the token could belong to a session
    /// opened before a demotion, or to a session that should have ended at a block. Either way
    /// the token is stale and the answer is no.
    ///
    /// The requirement is also checked against the row even when the claims match, so a role
    /// that was changed in the database cannot be honoured on the strength of a token that
    /// predates the change. There is no caching here: a cached answer has a lifetime, and a
    /// stale one is exactly the failure being refused.
    /// </remarks>
    public sealed class RoleRequirementHandler : AuthorizationHandler<RoleRequirement>
    {
        private readonly IAccessChecker _accessChecker;
        private readonly ILogger<RoleRequirementHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RoleRequirementHandler"/> class.
        /// </summary>
        /// <param name="accessChecker">Reader of the current state of an account.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public RoleRequirementHandler(
            IAccessChecker accessChecker,
            ILogger<RoleRequirementHandler> logger)
        {
            _accessChecker = accessChecker;
            _logger = logger;
        }

        /// <inheritdoc />
        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            RoleRequirement requirement)
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                // Left to the authentication handler, which answers a different status and a
                // different code: there being no token at all is not the same problem as
                // holding a token that is no longer good.
                return;
            }

            var subject = context.User.FindFirst(TokenClaimTypes.Subject)?.Value
                ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(subject, out var userId))
            {
                _logger.LogWarning(
                    "A token carried no usable account identifier, so the request was refused.");

                return;
            }

            var standing = await _accessChecker.GetStandingAsync(userId);

            if (!standing.IsActive)
            {
                _logger.LogInformation(
                    "Refused a request from account {UserId}: it is {Status}.",
                    userId,
                    standing.Status?.ToString() ?? "missing");

                return;
            }

            var claimedRole = ReadRole(context.User);

            if (claimedRole is not { } roleFromToken)
            {
                _logger.LogWarning(
                    "A token for account {UserId} carried no usable role claim, so the request " +
                    "was refused.",
                    userId);

                return;
            }

            if (roleFromToken != standing.Role)
            {
                _logger.LogInformation(
                    "Refused a request from account {UserId}: its token claims {TokenRole} while " +
                    "the account holds {ActualRole}.",
                    userId,
                    roleFromToken,
                    standing.Role);

                return;
            }

            if (standing.Role != requirement.Role)
            {
                _logger.LogInformation(
                    "Refused a request from account {UserId} in role {Role}: that endpoint accepts " +
                    "{RequiredRole} only.",
                    userId,
                    standing.Role,
                    requirement.Role);

                return;
            }

            context.Succeed(requirement);
        }

        /// <summary>
        /// The role the token carries, or null when it carries none or carries something that is
        /// not a role.
        /// </summary>
        private static Fluxy.Core.Models.Users.UserRole? ReadRole(System.Security.Claims.ClaimsPrincipal user)
        {
            var value = user.FindFirst(TokenClaimTypes.Role)?.Value
                ?? user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            // A claim naming a value that is not a role is treated as no role at all, rather
            // than as the nearest member. Casting an undefined number into the enum would make
            // a token signed with a nonsense role pass a check it has no business passing.
            return int.TryParse(value, out var role)
                && Enum.IsDefined(typeof(Fluxy.Core.Models.Users.UserRole), role)
                ? (Fluxy.Core.Models.Users.UserRole)role
                : null;
        }
    }
}
