using Fluxy.Application.Services;
using Fluxy.Application.Services.Authentication;
using Fluxy.Application.Services.Authorization;
using Fluxy.Application.Services.Email;
using Fluxy.Application.Services.GeoIp;
using Fluxy.Application.Services.Profile;
using Fluxy.Application.Services.Registration;
using Fluxy.Application.Services.Security;
using Fluxy.Application.Services.Throttling;
using Fluxy.Core.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxy.Application
{
    /// <summary>
    /// Registration helpers for the application layer. Keeping them here means the API layer
    /// wires services without knowing which concrete implementations exist.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the services of this layer.
        /// </summary>
        /// <param name="services">Service collection to register the services in.</param>
        /// <param name="configuration">
        /// Configuration the options types are bound from. Optional, so the layer can be wired
        /// without a configuration source during a unit test.
        /// </param>
        /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services,
            IConfiguration? configuration = null)
        {
            services.AddScoped<IUserSeeder, DefaultUserSeeder>();
            services.AddScoped<IRegistrationService, RegistrationService>();
            services.AddScoped<IAccessChecker, AccessChecker>();

            // Both are scoped for the reason every service that reads and writes through the
            // context is: a DbContext is not safe to share between requests.
            services.AddScoped<IProfileService, ProfileService>();
            services.AddScoped<IPasswordResetService, PasswordResetService>();

            // The sign-in service is scoped because it reads and writes through the context.
            // The token service is scoped with it rather than shared, for the same reason: it
            // writes a row per issued token.
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped<ITokenService, JwtTokenService>();

            // The guard check runs on the sign-in and refresh paths, next to the context it
            // reads the allow lists through, so it lives in a scope like the services it serves.
            services.AddScoped<ILoginGuardService, LoginGuardService>();

            // The throttle counts attempts for every request that reaches an endpoint, so it is
            // shared. It holds no per-request state, which is what lets it live next to the redis
            // connection rather than in a scope.
            services.AddSingleton<IAttemptThrottle, RedisAttemptThrottle>();

            // The GeoIP databases are opened once and shared between requests. The readers are
            // safe to share, and holding them in a scope would reopen the files per request.
            services.AddSingleton<IGeoIpResolver, MaxMindGeoIpResolver>();

            // Checked on every authorized request, so it is asked on every request. It holds
            // only the redis connection and its own logger, and lives next to the connection for
            // the same reason the throttle does.
            services.AddSingleton<ITokenRevocationStore, RedisTokenRevocationStore>();

            // The mailer is shared too. MailKit opens a connection per message, so there is no
            // connection to keep warm between requests.
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
            services.AddSingleton<IRecaptchaValidator, GoogleRecaptchaValidator>();

            // A single client for the reCAPTCHA check, whose handler is where a verification
            // service outage turns into a slow request rather than a failed one.
            services.AddHttpClient(nameof(GoogleRecaptchaValidator), client =>
                client.Timeout = TimeSpan.FromSeconds(10));

            // The clock is injected rather than read from DateTimeOffset so the code lifetime can
            // be exercised without waiting for a real window to pass.
            services.AddSingleton(TimeProvider.System);

            if (configuration is not null)
            {
                services.Configure<EmailOptions>(
                    configuration.GetSection(EmailOptions.SectionName));
                services.Configure<RecaptchaOptions>(
                    configuration.GetSection(RecaptchaOptions.SectionName));
                services.Configure<RegistrationOptions>(
                    configuration.GetSection(RegistrationOptions.SectionName));
                services.Configure<AuthenticationOptions>(
                    configuration.GetSection(AuthenticationOptions.SectionName));
                services.Configure<GeoIpOptions>(
                    configuration.GetSection(GeoIpOptions.SectionName));
            }

            return services;
        }

        /// <summary>
        /// Runs <see cref="IUserSeeder.SeedAsync"/> in a scope of its own, so it can be called
        /// from the composition root without resolving anything there.
        /// </summary>
        /// <param name="services">Provider built by the host.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// This is an explicit call rather than an <see cref="IHostedService"/> on purpose:
        /// hosted services start in registration order, so the web server could already accept
        /// requests while the schema is still missing. An awaited call runs before
        /// <c>app.Run()</c> and before the first request is served.
        /// </remarks>
        public static async Task SeedDatabaseAsync(
            this IServiceProvider services,
            CancellationToken cancellationToken = default)
        {
            await using var scope = services.CreateAsyncScope();

            var seeder = scope.ServiceProvider.GetRequiredService<IUserSeeder>();

            await seeder.SeedAsync(cancellationToken);
        }
    }
}
