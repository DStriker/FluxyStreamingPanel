using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxy.API.Configuration
{
    /// <summary>
    /// Wiring of the browser policy that lets the frontend reach this API.
    /// </summary>
    public static class CorsExtensions
    {
        /// <summary>Configuration section the allowed origins are read from.</summary>
        public const string OriginsSectionName = "Cors:AllowedOrigins";

        /// <summary>Name of the registered policy.</summary>
        public const string PolicyName = "fluxy-frontend";

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

        private static string[] ReadOrigins(IConfiguration configuration)
            => configuration.GetSection(OriginsSectionName).Get<string[]>() ?? [];
    }
}