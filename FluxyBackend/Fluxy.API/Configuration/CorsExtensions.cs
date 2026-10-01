using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxy.API.Configuration
{
    /// <summary>
    /// Wiring of the browser policy that lets the frontend reach this API.
    /// </summary>
    public static class CorsExtensions
    {
        /// <summary>
        /// Header the Vite dev proxy sets on everything it forwards, carrying the scheme the
        /// browser actually used.
        /// </summary>
        /// <remarks>
        /// Its presence is what tells the pipeline that a request already came through a
        /// terminator, so no redirect or cookie hardening applies to it. See the
        /// <c>UseWhen</c> branch around <c>UseHttpsRedirection</c> in <c>Program.cs</c>, and
        /// <c>vite.config.js</c> on the frontend side, which has to send it.
        /// </remarks>
        public const string ProxyProtocolHeader = "X-Forwarded-Proto";

        /// <summary>Configuration section the allowed origins are read from.</summary>
        public const string OriginsSectionName = "Cors:AllowedOrigins";

        /// <summary>
        /// Key that switches the browser policy off entirely. Absent or unparseable means on.
        /// </summary>
        public const string EnabledKey = "Cors:Enabled";

        /// <summary>Name of the registered policy.</summary>
        public const string PolicyName = "fluxy-frontend";

        /// <summary>
        /// Whether the browser policy is on. Defaults to on when the key is absent, so an
        /// installation that never heard of the key behaves as before.
        /// </summary>
        /// <param name="configuration">Configuration to read.</param>
        public static bool IsEnabled(IConfiguration configuration)
            => !bool.TryParse(configuration[EnabledKey], out var enabled) || enabled;

        /// <summary>
        /// Registers the one browser policy this API has.
        /// </summary>
        /// <param name="services">Service collection to register the policy in.</param>
        /// <param name="configuration">
        /// Configuration carrying <c>Cors:AllowedOrigins</c>, a list of exact origins such as
        /// <c>http://localhost:5173</c>. An entry has to include the scheme, and a host that is
        /// not <c>localhost</c> has to include its port, because the browser compares the whole
        /// origin and nothing less.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// The configured list contains the wildcard <c>*</c>, which cannot be combined with the
        /// credentials this API requires.
        /// </exception>
        /// <remarks>
        /// Credentials are allowed, and that is not optional here: the frontend authenticates
        /// with cookies, and a browser drops those cross origin unless the answer says so. It is
        /// also the reason a wildcard is refused rather than trimmed - a wildcard plus credentials
        /// is a configuration the browser rejects anyway, and failing here names the line that is
        /// wrong instead of leaving a client to work out why every request fails.
        ///
        /// An empty list is not an error. It means no browser may call this API, which is the
        /// right answer for an installation that ships no frontend. The composition root warns
        /// about it, because a developer who expects the frontend to work would otherwise only
        /// find out from the browser's console.
        /// </remarks>
        public static IServiceCollection AddFrontendCors(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Switched off on purpose: no policy is registered at all, and `UseFrontendCors`
            // correspondingly does not ask for one. A wildcard is not the way to express this -
            // a browser refuses `*` together with credentials, and the request would still fail,
            // only with a different and more confusing message.
            if (!IsEnabled(configuration))
            {
                return services;
            }

            var origins = ReadOrigins(configuration);

            if (origins.Any(origin => origin is "*" or "null"))
            {
                throw new InvalidOperationException(
                    $"'{OriginsSectionName}' contains a wildcard, which cannot be combined with " +
                    "the credentials this API uses. List the exact origins instead, for example " +
                    "[\"http://localhost:5173\"].");
            }

            services.AddCors(options => options.AddPolicy(
                PolicyName,
                policy => policy
                    .WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials()));

            return services;
        }

        /// <summary>
        /// Whether at least one origin is allowed, which is what tells the composition root that
        /// no browser can reach this installation.
        /// </summary>
        /// <param name="configuration">Configuration to read from.</param>
        public static bool HasAllowedOrigins(IConfiguration configuration)
            => ReadOrigins(configuration).Length > 0;

        /// <summary>
        /// Adds the browser policy to the pipeline, unless it was switched off.
        /// </summary>
        /// <param name="app">Application to add the middleware to.</param>
        /// <remarks>
        /// Paired with <see cref="AddFrontendCors"/>: calling <c>UseCors</c> for a policy that was
        /// never registered throws at start-up, which is why the decision has to be made in both
        /// places and read from the same key.
        /// </remarks>
        public static void UseFrontendCors(this WebApplication app)
        {
            if (!IsEnabled(app.Configuration))
            {
                return;
            }

            app.UseCors(PolicyName);
        }

        private static string[] ReadOrigins(IConfiguration configuration)
            => configuration.GetSection(OriginsSectionName).Get<string[]>() ?? [];
    }
}