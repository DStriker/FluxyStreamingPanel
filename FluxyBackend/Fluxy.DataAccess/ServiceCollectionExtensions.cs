using Fluxy.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
    }
}
