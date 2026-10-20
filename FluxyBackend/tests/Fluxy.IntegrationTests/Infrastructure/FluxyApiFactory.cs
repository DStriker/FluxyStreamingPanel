using Fluxy.API;
using Fluxy.Application.Users.Utils;
using Fluxy.Core.Email.Interfaces;
using Fluxy.Core.GeoIp.Interfaces;
using Fluxy.Core.Security.Interfaces;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Interfaces;
using Fluxy.IntegrationTests.Infrastructure.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Fluxy.IntegrationTests.Infrastructure
{
    /// <summary>
    /// A host built from the real composition root, with its external dependencies replaced by
    /// deterministic fakes and its two connection strings pointed at Testcontainers.
    /// </summary>
    public sealed class FluxyApiFactory : WebApplicationFactory<Program>
    {
        public const string SigningKey =
            "5f1d3c7a9b2e4068a1c5e9f3d7b0c4a8e2f6a9c1d5b8e0f4a7c3d6b9e1f5a8c2";

        private readonly string _postgresConnectionString;
        private readonly string _redisConnectionString;
        private readonly RecordingEmailSender _email;
        private readonly FakeRecaptchaValidator _recaptcha;
        private readonly FakeGeoIpResolver _geoIp;
        private readonly FakeTimeProvider _clock;
        private readonly IReadOnlyDictionary<string, string?> _extraSettings;
        private readonly Dictionary<string, string?> _previousEnvironment = new(StringComparer.Ordinal);
        private bool _environmentRestored;

        public FluxyApiFactory(
            string postgresConnectionString,
            string redisConnectionString,
            RecordingEmailSender email,
            FakeRecaptchaValidator recaptcha,
            FakeGeoIpResolver geoIp,
            FakeTimeProvider clock,
            IReadOnlyDictionary<string, string?>? extraSettings = null)
        {
            _postgresConnectionString = postgresConnectionString;
            _redisConnectionString = redisConnectionString;
            _email = email;
            _recaptcha = recaptcha;
            _geoIp = geoIp;
            _clock = clock;
            _extraSettings = extraSettings ?? new Dictionary<string, string?>();

            // Minimal hosting reads its configuration while Program.Main is still running, before
            // WebApplicationFactory's deferred ConfigureAppConfiguration callback is applied. The
            // values therefore have to be visible to WebApplication.CreateBuilder itself: setting
            // them as environment variables is early enough, and environment variables outrank the
            // .env file that Program inserts at position zero.
            ApplyEnvironment();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                RestoreEnvironment();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            RestoreEnvironment();
            await base.DisposeAsync();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(Settings());
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(_email);

                services.RemoveAll<IRecaptchaValidator>();
                services.AddSingleton<IRecaptchaValidator>(_recaptcha);

                services.RemoveAll<IGeoIpResolver>();
                services.AddSingleton<IGeoIpResolver>(_geoIp);

                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_clock);

                services.RemoveAll<IUserSeeder>();
                services.AddScoped<IUserSeeder, TestUserSeeder>();
            });
        }

        private void ApplyEnvironment()
        {
            foreach (var (variable, value) in EnvironmentValues())
            {
                _previousEnvironment[variable] = Environment.GetEnvironmentVariable(variable);
                Environment.SetEnvironmentVariable(variable, value);
            }
        }

        /// <summary>
        /// Puts the process environment back the way it was before this factory existed.
        /// </summary>
        /// <remarks>
        /// The variables are process-wide, while a test may create a second factory with tighter
        /// limits (the rate-limit tests do). Without this restore, those limits would outlive the
        /// test that asked for them and leak into every later test in the same process - a failure
        /// that depends on test order rather than on the code under test.
        /// </remarks>
        private void RestoreEnvironment()
        {
            if (_environmentRestored)
            {
                return;
            }

            _environmentRestored = true;

            foreach (var pair in _previousEnvironment)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }

            _previousEnvironment.Clear();
        }

        private IEnumerable<KeyValuePair<string, string?>> EnvironmentValues()
        {
            yield return new KeyValuePair<string, string?>(
                "ASPNETCORE_ENVIRONMENT",
                "Testing");

            foreach (var pair in Settings())
            {
                yield return new KeyValuePair<string, string?>(
                    pair.Key.Replace(":", "__", StringComparison.Ordinal),
                    pair.Value);
            }
        }

        private Dictionary<string, string?> Settings()
        {
            var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ConnectionStrings:Postgres"] = _postgresConnectionString,
                ["ConnectionStrings:Redis"] = _redisConnectionString,
                ["Auth:SigningKey"] = SigningKey,
                ["Auth:Issuer"] = "fluxy-tests",
                ["Auth:Audience"] = "fluxy-api-tests",
                ["Cors:Enabled"] = "false",
                ["Recaptcha:SecretKey"] = string.Empty,
                ["Registration:CodeLength"] = "6",
                ["Registration:CodeLifetime"] = "00:15:00",
                ["GeoIp:CityDbPath"] = string.Empty,
                ["GeoIp:AsnDbPath"] = string.Empty,
                ["RateLimit:RegisterLimit"] = "1000",
                ["RateLimit:RegisterWindow"] = "00:15:00",
                ["RateLimit:ConfirmLimit"] = "1000",
                ["RateLimit:ConfirmWindow"] = "00:15:00",
                ["RateLimit:LoginLimit"] = "1000",
                ["RateLimit:LoginWindow"] = "00:15:00",
                ["RateLimit:RefreshLimit"] = "1000",
                ["RateLimit:RefreshWindow"] = "00:15:00",
                ["RateLimit:PreferenceLimit"] = "1000",
                ["RateLimit:PreferenceWindow"] = "00:15:00",
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning"
            };

            foreach (var pair in _extraSettings)
            {
                settings[pair.Key] = pair.Value;
            }

            return settings;
        }

        // Kept for readability at call sites that are about a role rather than a group.
        public static Guid BaseGroup(UserRole role) => Core.Users.Utils.BaseUserGroups.ForRole(role);
    }
}
