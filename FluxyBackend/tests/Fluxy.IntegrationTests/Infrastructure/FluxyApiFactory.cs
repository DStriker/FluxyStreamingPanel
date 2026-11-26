using Fluxy.API;
using Fluxy.Application.Servers.Services;
using Fluxy.Application.Servers.Utils;
using Fluxy.Application.Users.Utils;
using Fluxy.Core.Email.Interfaces;
using Fluxy.Core.GeoIp.Interfaces;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Security.Interfaces;
using Fluxy.Core.Servers.Interfaces;
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
        private readonly FakeServerStatsProbe _statsProbe;
        private readonly FakeTimeProvider _clock;
        private readonly FakeDatabaseBackupTool _backupTool;
        private readonly string _backupDirectory;
        private readonly IReadOnlyDictionary<string, string?> _extraSettings;
        private readonly bool _keepLocalMainProbe;
        private int _localReadings;
        private readonly Dictionary<string, string?> _previousEnvironment = new(StringComparer.Ordinal);
        private bool _environmentRestored;

        public FluxyApiFactory(
            string postgresConnectionString,
            string redisConnectionString,
            RecordingEmailSender email,
            FakeRecaptchaValidator recaptcha,
            FakeGeoIpResolver geoIp,
            FakeTimeProvider clock,
            IReadOnlyDictionary<string, string?>? extraSettings = null,
            FakeDatabaseBackupTool? backupTool = null,
            string? backupDirectory = null,
            bool keepLocalMainProbe = false)
        {
            _postgresConnectionString = postgresConnectionString;
            _redisConnectionString = redisConnectionString;
            _email = email;
            _recaptcha = recaptcha;
            _geoIp = geoIp;
            _statsProbe = new FakeServerStatsProbe();
            _clock = clock;
            _backupTool = backupTool ?? new FakeDatabaseBackupTool();
            _backupDirectory = backupDirectory ?? NewBackupDirectory();
            _extraSettings = extraSettings ?? new Dictionary<string, string?>();
            _keepLocalMainProbe = keepLocalMainProbe;

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

                // The dashboard probes every server's machine; a real probe would dial a
                // Testcontainers-internal address. The fake is exposed as a property so a test can
                // decide which servers answer and prove the cache performed no extra probes.
                //
                // RemoveAll only removes the *unkeyed* IServerStatsProbe - the decorator. The
                // remote HTTP probe is registered under SelfServerStatsProbe.RemoteKey and stays,
                // unused, because the fake below becomes the only IServerStatsProbe the dashboard
                // resolves. Every default-factory dashboard test therefore exercises the fake for
                // every row including the main one; the local sampler and the decorator are only
                // exercised by a factory built with keepLocalMainProbe.
                if (_keepLocalMainProbe)
                {
                    services.RemoveAllKeyed<IServerStatsProbe>(SelfServerStatsProbe.RemoteKey);
                    services.AddKeyedSingleton<IServerStatsProbe>(
                        SelfServerStatsProbe.RemoteKey,
                        _statsProbe);

                    // The decorator answers the main row from this sampler. The reader is
                    // deterministic, so the test does not depend on the machine it runs on; the
                    // first pair of readings is the cold start's own short window, which answers
                    // immediately (after the documented 250 ms wait).
                    services.RemoveAll<SelfServerStatsSampler>();
                    services.AddSingleton<SelfServerStatsSampler>(
                        _ => new SelfServerStatsSampler(_clock, () => LocalReading()));
                }
                else
                {
                    services.RemoveAll<IServerStatsProbe>();
                    services.AddSingleton<IServerStatsProbe>(_statsProbe);
                }

                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_clock);

                // The dump/restore tool is replaced rather than pointed at Docker: the suite's
                // PostgreSQL lives in a Testcontainers container whose name the host does not
                // know, and a real restore would replace the shared schema mid-run.
                services.RemoveAll<IDatabaseBackupTool>();
                services.AddSingleton<IDatabaseBackupTool>(_backupTool);

                services.RemoveAll<IUserSeeder>();
                services.AddScoped<IUserSeeder, TestUserSeeder>();

                // The identity the main row is seeded from. A fake keeps the seeded values the
                // same on every machine, which is what lets the wire test assert them.
                services.RemoveAll<IServerIdentityProvider>();
                services.AddSingleton<IServerIdentityProvider, FakeServerIdentityProvider>();
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
                ["Backups:ContainerName"] = "postgres",
                ["Backups:Directory"] = _backupDirectory,
                ["Backups:TickSeconds"] = "60",
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

        /// <summary>The statistics probe every dashboard test observes.</summary>
        public FakeServerStatsProbe StatsProbe => _statsProbe;

        /// <summary>
        /// One deterministic reading of "this process", for a factory built with
        /// <c>keepLocalMainProbe</c>.
        /// </summary>
        /// <remarks>
        /// Each reading advances by <see cref="SelfServerStatsSampler.MinimumWindow"/> on top of the
        /// fake clock, so a window measured between two readings is always inside the sampler's
        /// bounds and the arithmetic is the same on every reading. The first dashboard build is a
        /// cold start and takes the pair itself, which is what the main row now answers from.
        /// </remarks>
        private SelfStatsReading LocalReading()
        {
            var index = Interlocked.Increment(ref _localReadings);
            var captured = _clock.GetUtcNow() + (SelfServerStatsSampler.MinimumWindow * index);

            return new SelfStatsReading
            {
                CapturedAt = captured,
                ProcessorTime = TimeSpan.FromSeconds(index) / 4,
                WorkingSetBytes = 100,
                AvailableMemoryBytes = 1_000,
                DiskUsedBytes = 100,
                DiskTotalBytes = 200,
                StartedAt = captured.AddSeconds(-900),
                BytesReceived = index * 125_000,
                BytesSent = index * 125_000
            };
        }

        /// <summary>A fresh directory under the temp path for a factory that was not given one.</summary>
        private static string NewBackupDirectory()
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                "fluxy-backups-tests-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(path);

            return path;
        }
    }
}
