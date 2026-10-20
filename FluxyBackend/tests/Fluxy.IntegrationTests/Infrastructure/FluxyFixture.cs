using Fluxy.Core.Persistence.Interfaces;
using Fluxy.IntegrationTests.Infrastructure.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Respawn;
using Respawn.Graph;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Fluxy.IntegrationTests.Infrastructure
{
    /// <summary>
    /// One pair of containers and one host for the whole integration collection, reset between
    /// tests. xUnit runs the tests of a collection sequentially, so the shared database is never
    /// observed from two tests at once.
    /// </summary>
    public sealed class FluxyFixture : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.6")
            .WithDatabase("fluxy_tests")
            .WithUsername("fluxy")
            .WithPassword("fluxy-tests-password")
            .Build();

        private readonly RedisContainer _redis = new RedisBuilder("redis:8.6").Build();

        private Respawner _respawner = null!;
        private IConnectionMultiplexer _redisConnection = null!;

        public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

        public RecordingEmailSender Email { get; } = new();

        public FakeRecaptchaValidator Recaptcha { get; } = new();

        public FakeGeoIpResolver GeoIp { get; } = new();

        public FluxyApiFactory Factory { get; private set; } = null!;

        public string PostgresConnectionString => _postgres.GetConnectionString();

        public string RedisConnectionString => _redis.GetConnectionString();

        public async ValueTask InitializeAsync()
        {
            await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

            Factory = new FluxyApiFactory(
                PostgresConnectionString,
                RedisConnectionString,
                Email,
                Recaptcha,
                GeoIp,
                Clock);

            // Creating a client is what starts the host. The program's own seeder may or may not
            // have run by the time the factory returns, so the schema is checked explicitly below.
            using (Factory.CreateClient())
            {
            }

            await using (var scope = Factory.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider
                    .GetRequiredService<IDatabaseInitializer>()
                    .MigrateAsync();

                await TestDataSeeder.EnsureBaseGroupsAsync(scope.ServiceProvider);
            }

            await using (var connection = new NpgsqlConnection(PostgresConnectionString))
            {
                await connection.OpenAsync();

                _respawner = await Respawner.CreateAsync(
                    connection,
                    new RespawnerOptions
                    {
                        DbAdapter = DbAdapter.Postgres,
                        TablesToIgnore = [new Table("public", "__EFMigrationsHistory")]
                    });
            }

            _redisConnection = await ConnectionMultiplexer.ConnectAsync(RedisOptions());
        }

        public async Task ResetAsync()
        {
            Email.Clear();
            GeoIp.Clear();
            Recaptcha.Outcome = Fluxy.Core.Security.Enums.RecaptchaValidationOutcome.Passed;

            await using (var connection = new NpgsqlConnection(PostgresConnectionString))
            {
                await connection.OpenAsync();
                await _respawner.ResetAsync(connection);
            }
            await _redisConnection.GetDatabase().ExecuteAsync("FLUSHDB");

            await using var scope = Factory.Services.CreateAsyncScope();
            await TestDataSeeder.EnsureBaseGroupsAsync(scope.ServiceProvider);
        }

        public HttpClient CreateClient()
            => Factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

        private ConfigurationOptions RedisOptions()
        {
            var options = ConfigurationOptions.Parse(RedisConnectionString);
            options.AllowAdmin = true;
            options.AbortOnConnectFail = false;

            return options;
        }

        /// <summary>
        /// Opens a connection to the test database for fixtures that are cheaper to plant with SQL
        /// than through the API.
        /// </summary>
        public async Task<NpgsqlConnection> OpenConnectionAsync()
        {
            var connection = new NpgsqlConnection(PostgresConnectionString);
            await connection.OpenAsync();

            return connection;
        }

        public async ValueTask DisposeAsync()
        {
            if (_redisConnection is not null)
            {
                await _redisConnection.DisposeAsync();
            }

            await Factory.DisposeAsync();
            await _redis.DisposeAsync();
            await _postgres.DisposeAsync();
        }
    }
}
