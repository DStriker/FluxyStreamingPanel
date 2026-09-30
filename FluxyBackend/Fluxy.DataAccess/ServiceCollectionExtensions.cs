using Fluxy.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Fluxy.DataAccess
{
    /// <summary>
    /// Registration helpers for the data access layer.
    /// Keeping them here means the API layer does not have to reference Npgsql
    /// or EF Core directly.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Name of the connection string the data access layer reads.
        /// It resolves to the configuration key <c>ConnectionStrings:Postgres</c>.
        /// </summary>
        public const string PostgresConnectionName = "Postgres";

        /// <summary>
        /// Name of the connection string the redis registration reads.
        /// It resolves to the configuration key <c>ConnectionStrings:Redis</c>.
        /// </summary>
        public const string RedisConnectionName = "Redis";

        /// <summary>
        /// Configuration switch that decides whether a redis outage is fatal.
        /// </summary>
        /// <remarks>
        /// Redis holds only the attempt counters, and every consumer of them already falls back
        /// to process memory, so an unreachable server is a degraded installation rather than a
        /// broken one. The default therefore lets the application start. Setting the value to
        /// <c>true</c> turns a missing connection string into a startup error, which is only
        /// sensible when several instances have to agree on the counters.
        /// </remarks>
        public const string RedisRequiredConfigurationKey = "Redis:Required";

        /// <summary>
        /// Registers <see cref="FluxyDbContext"/> with the PostgreSQL provider.
        /// </summary>
        /// <param name="services">Service collection to register the context in.</param>
        /// <param name="configuration">
        /// Configuration that provides <c>ConnectionStrings:Postgres</c>. In this solution it
        /// comes from the compose <c>.env</c> file, which is loaded by the API at startup.
        /// </param>
        /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// The connection string is missing or empty. Failing here surfaces a broken
        /// configuration at startup instead of on the first query.
        /// </exception>
        public static IServiceCollection AddDataAccess(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString(PostgresConnectionName);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string 'ConnectionStrings:{PostgresConnectionName}' is not configured. " +
                    "Provide it with user-secrets " +
                    "(\"dotnet user-secrets --project Fluxy.API set ConnectionStrings:Postgres \\\"Host=...\\\"\"), " +
                    "with an environment variable, or through the compose .env file, which has to contain " +
                    "POSTGRES_DB, POSTGRES_USER and POSTGRES_PASSWORD (see scripts\\init-secrets.ps1).");
            }

            services.AddDbContext<FluxyDbContext>(options => options.UseNpgsql(connectionString));

            return services;
        }

        /// <summary>
        /// Registers <see cref="IConnectionMultiplexer"/> against the compose redis.
        /// </summary>
        /// <param name="services">Service collection to register the connection in.</param>
        /// <param name="configuration">
        /// Configuration that provides <c>ConnectionStrings:Redis</c>. In this solution it is
        /// composed from the <c>REDIS_USER</c> and <c>REDIS_PASSWORD</c> secrets of the
        /// compose <c>.env</c> file.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// The connection string is missing and <c>Redis:Required</c> is set to <c>true</c>.
        /// </exception>
        /// <remarks>
        /// The connection string is validated and parsed at startup, but the socket itself is
        /// not required to be open: <c>AbortOnConnectFail</c> is forced off, so a redis that is
        /// still starting, or not running at all, does not prevent the host from starting and
        /// does not throw from the constructor. Consumers check
        /// <see cref="ConnectionMultiplexer.IsConnected"/> and degrade to process memory, which
        /// keeps a registration request serving when redis is down. The value is passed through
        /// to StackExchange.Redis first, so an installation that really needs the stricter
        /// default can still set <c>abortConnect=true</c> in its own connection string.
        /// </remarks>
        public static IServiceCollection AddRedis(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString(RedisConnectionName);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                if (IsRequired(configuration))
                {
                    throw new InvalidOperationException(
                        $"Connection string 'ConnectionStrings:{RedisConnectionName}' is not " +
                        $"configured while '{RedisRequiredConfigurationKey}' is true. Provide it " +
                        "with user-secrets, with an environment variable, or through the compose " +
                        ".env file, which has to contain REDIS_USER and REDIS_PASSWORD (see " +
                        "scripts\\init-secrets.ps1).");
                }

                return services;
            }

            ConfigurationOptions options;

            try
            {
                options = ConfigurationOptions.Parse(connectionString);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    $"Connection string 'ConnectionStrings:{RedisConnectionName}' is malformed and " +
                    "could not be parsed by StackExchange.Redis.",
                    exception);
            }

            // A malformed value is always a configuration mistake worth stopping for, even when
            // redis is not required: a string that parses into nonsense would otherwise fail
            // later, per request, far from the setting that caused it.
            options.AbortOnConnectFail = false;

            services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(options));

            return services;
        }

        /// <summary>
        /// Whether a <see cref="IConnectionMultiplexer"/> is registered and currently usable.
        /// </summary>
        /// <param name="multiplexer">
        /// Multiplexer to inspect, or null when no connection string was configured.
        /// </param>
        /// <returns>True when there is a connection and it is up.</returns>
        /// <remarks>
        /// The throttle implementations use this to decide between the shared counters and
        /// their local fallback, which is why the check lives next to the registration.
        /// </remarks>
        public static bool IsRedisAvailable(IConnectionMultiplexer? multiplexer)
            => multiplexer is { IsConnected: true };

        /// <summary>
        /// Whether redis has been declared mandatory. Read by hand rather than through
        /// <c>GetValue&lt;bool&gt;</c> so the data access layer keeps its single configuration
        /// dependency instead of gaining the binder package for one flag.
        /// </summary>
        private static bool IsRequired(IConfiguration configuration)
            => bool.TryParse(configuration[RedisRequiredConfigurationKey], out var required)
                && required;
    }
}
