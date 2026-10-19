using Fluxy.Core.Abstractions;
using Fluxy.Core.Abstractions.Repositories;
using Fluxy.Core.GeoIp.Interfaces;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Security.Interfaces;
using Fluxy.Core.Users.Repositories;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Infrastructure;
using Fluxy.DataAccess.Infrastructure.GeoIp;
using Fluxy.DataAccess.Infrastructure.Redis;
using Fluxy.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Fluxy.DataAccess
{
    /// <summary>
    /// Registration helpers for the data access layer.
    /// Keeping them here means the API layer does not have to reference Npgsql,
    /// EF Core or StackExchange.Redis directly.
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
        /// Redis holds the attempt counters and the identifiers of revoked sessions, and both
        /// consumers already degrade on their own when it is unreachable, so a missing server is
        /// a degraded installation rather than a broken one. The default therefore lets the
        /// application start. Setting the value to <c>true</c> turns a missing connection string
        /// into a startup error, which is only sensible when several instances have to agree on
        /// the counters.
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

            // The repositories and the unit of work are scoped with the context they wrap: a
            // DbContext is not safe to share between requests, and the tracker that carries a
            // staged write from one repository call to the next belongs to one request only.
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<IPendingChangeRepository, PendingChangeRepository>();
            services.AddScoped<ILoginGuardRepository, LoginGuardRepository>();
            services.AddScoped<IUserGroupRepository, UserGroupRepository>();
            services.AddScoped<IContentCategoryRepository, ContentCategoryRepository>();
            services.AddScoped<IVideoStreamRepository, VideoStreamRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

            return services;
        }

        /// <summary>
        /// Registers the shared redis connection and the stores that use it.
        /// </summary>
        /// <param name="services">Service collection to register the redis infrastructure in.</param>
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
        /// does not throw from the constructor. The shared connection wrapper is registered in
        /// both configurations, so the stores resolve even when there is no connection string
        /// and degrade to process memory or fail-open instead of failing dependency injection.
        /// Consumers check availability through that wrapper, which keeps a registration request
        /// serving when redis is down.
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
            }
            else
            {
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

                // A malformed value is always a configuration mistake worth stopping for, even
                // when redis is not required: a string that parses into nonsense would
                // otherwise fail later, per request, far from the setting that caused it.
                options.AbortOnConnectFail = false;

                services.AddSingleton<IConnectionMultiplexer>(
                    _ => ConnectionMultiplexer.Connect(options));
            }

            services.AddSingleton<RedisConnection>(provider => new RedisConnection(
                provider.GetService<IConnectionMultiplexer>(),
                provider.GetRequiredService<ILogger<RedisConnection>>()));

            services.AddSingleton<IAttemptThrottle, RedisAttemptThrottle>();
            services.AddSingleton<ITokenRevocationStore, RedisTokenRevocationStore>();

            return services;
        }

        /// <summary>
        /// Registers the reader of the local MaxMind databases.
        /// </summary>
        /// <param name="services">Service collection to register the resolver in.</param>
        /// <param name="configuration">
        /// Configuration that provides the <c>GeoIp</c> section with the paths of the city and
        /// the ASN database.
        /// </param>
        /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
        /// <remarks>
        /// The databases are opened on first use rather than at startup, so an installation
        /// without the files still starts: the resolver answers "unknown" for every address and
        /// the guard refuses the accounts that asked to be protected. Nothing here throws for a
        /// missing file, because a host without the files is a configuration it is allowed to
        /// have.
        /// </remarks>
        public static IServiceCollection AddGeoIp(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<GeoIpOptions>(configuration.GetSection(GeoIpOptions.SectionName));

            // The readers are opened once and shared between requests. They are safe to share,
            // and holding them in a scope would reopen the files per request.
            services.AddSingleton<IGeoIpResolver, MaxMindGeoIpResolver>();

            return services;
        }

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
