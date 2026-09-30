using Fluxy.Application.Services;
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
        /// Registers the services of this layer, currently the default user seeder.
        /// </summary>
        /// <param name="services">Service collection to register the services in.</param>
        /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IUserSeeder, DefaultUserSeeder>();

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