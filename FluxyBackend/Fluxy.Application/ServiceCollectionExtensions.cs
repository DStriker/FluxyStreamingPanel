using Fluxy.Application.Authentication.Services;
using Fluxy.Application.Authentication.Utils;
using Fluxy.Application.Content.Services;
using Fluxy.Application.Email.Options;
using Fluxy.Application.Email.Services;
using Fluxy.Application.Security.Options;
using Fluxy.Application.Security.Services;
using Fluxy.Application.Streams.Services;
using Fluxy.Application.Users.Options;
using Fluxy.Application.Users.Services;
using Fluxy.Core.Authentication.Interfaces;
using Fluxy.Core.Authentication.Options;
using Fluxy.Core.Content.Interfaces;
using Fluxy.Core.Email.Interfaces;
using Fluxy.Core.Security.Interfaces;
using Fluxy.Core.Streams.Interfaces;
using Fluxy.Core.Users.Interfaces;
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

            // The admin's view of the same rows: it reads and writes through the context like
            // every scoped service here, and ends sessions through the token service because a
            // blocked account or a changed password takes them with it.
            services.AddScoped<IUserAdminService, UserAdminService>();

            // The collective operations over the same rows. Its own service, because a bulk
            // request names a set and answers with an entry per row, and a client asking for
            // it must not have to know which single-row method it is built on.
            services.AddScoped<IUserBulkService, UserBulkService>();

            // The groups those accounts belong to are their own resource, with rules that have
            // no counterpart on an account row - a base group that may only be renamed, a group
            // with members that may not be deleted - so they are their own service rather than
            // extra methods on the one above.
            services.AddScoped<IUserGroupService, UserGroupService>();

            // The categories content is filed under are their own resource, with rules that
            // have no counterpart on an account or a group - a name unique within one kind of
            // content, an order rewritten wholesale by one request - so they are their own
            // service rather than extra methods on either of the two above.
            services.AddScoped<IContentCategoryService, ContentCategoryService>();

            // The streams filed under those categories are their own resource again, for the
            // same reason three times over: a stream has rules no category has - the type
            // owns the payload it may carry, a position is read rather than claimed - and a
            // service carrying both would answer "whose row?" for every call. Scoped because
            // it reads and writes through the context, like every other service here.
            services.AddScoped<IVideoStreamService, VideoStreamService>();

            // The machinery of a change that waits for a code, shared by the profile change and
            // the password reset. Scoped, because it holds the unit of work of the request.
            services.AddScoped<PendingChangeService>();

            // The sign-in service is scoped because it reads and writes through the context.
            // The token service is scoped with it rather than shared, for the same reason: it
            // writes a row per issued token.
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped<ITokenService, JwtTokenService>();

            // The guard check runs on the sign-in and refresh paths, next to the context it
            // reads the allow lists through, so it lives in a scope like the services it serves.
            services.AddScoped<ILoginGuardService, LoginGuardService>();

            // Reads one page of the account's stored sessions, so it reads through the context
            // like every other scoped service here and shares the resolver the sign-in path uses.
            services.AddScoped<ISessionHistoryService, SessionHistoryService>();

            // The same in every respect: one read of the account's own rows, with the same
            // resolver placed over them. It is a separate service rather than a second method
            // on the history one because the two answer opposite questions - one lists tokens
            // so a rotation is visible, the other lists sessions so one can be ended - and a
            // caller that wanted one should not be handed the other's shape to dig through.
            services.AddScoped<IActiveSessionService, ActiveSessionService>();

            // The same rows seen from the operator's side of them, and the reason this is its
            // own service rather than a second method on either above: those two answer a
            // question about one account, taken from the token, and have no shape for the
            // question that matters here - which accounts of the installation are this
            // caller's business at all. That answer comes in as `granted` and is the same rule
            // the accounts list is scoped by, so a person who may see an account may see where
            // it was signed in from. It is the only one of the three that writes, and only ever
            // by ending a session.
            services.AddScoped<IAdminSessionHistoryService, AdminSessionHistoryService>();

            // The lookups of one request, kept so the same address is not looked up twice. The
            // three services that place addresses on a map share it, and it is scoped rather
            // than singleton: the answers describe the request being served.
            services.AddScoped<GeoIpMemo>();

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
