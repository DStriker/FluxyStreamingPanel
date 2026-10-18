using Fluxy.API.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxy.API.Extensions
{
    /// <summary>
    /// Wiring of the attempt windows every endpoint that counts attempts reads its numbers from.
    /// </summary>
    /// <remarks>
    /// The five windows are bound and validated apart on purpose. They guard different steps -
    /// a registration, a confirmation, a sign-in, a refresh and a profile preference - and one
    /// message naming the section as a whole would leave the operator guessing which of the five
    /// numbers is the wrong one. Every check runs while the host starts rather than on the first
    /// request, so a limit of zero is a startup error naming the setting instead of an API that
    /// refuses every request of one kind and never says why.
    /// </remarks>
    public static class RateLimitExtensions
    {
        /// <summary>
        /// Binds the <c>RateLimit</c> section and validates each window from it.
        /// </summary>
        /// <param name="services">Service collection to register the options in.</param>
        /// <param name="configuration">Configuration the section is bound from.</param>
        /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
        public static IServiceCollection AddFluxyRateLimits(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddOptions<RateLimitOptions>()
                .Bind(configuration.GetSection(RateLimitOptions.SectionName))
                .Validate(
                    options => options.RegisterLimit > 0 && options.RegisterWindow > TimeSpan.Zero,
                    $"'{RateLimitOptions.SectionName}' needs a positive RegisterLimit and a " +
                    "RegisterWindow longer than zero.")
                .Validate(
                    options => options.ConfirmLimit > 0 && options.ConfirmWindow > TimeSpan.Zero,
                    $"'{RateLimitOptions.SectionName}' needs a positive ConfirmLimit and a " +
                    "ConfirmWindow longer than zero.")
                .Validate(
                    options => options.LoginLimit > 0 && options.LoginWindow > TimeSpan.Zero,
                    $"'{RateLimitOptions.SectionName}' needs a positive LoginLimit and a " +
                    "LoginWindow longer than zero.")
                .Validate(
                    options => options.RefreshLimit > 0 && options.RefreshWindow > TimeSpan.Zero,
                    $"'{RateLimitOptions.SectionName}' needs a positive RefreshLimit and a " +
                    "RefreshWindow longer than zero.")
                .Validate(
                    options => options.PreferenceLimit > 0 && options.PreferenceWindow > TimeSpan.Zero,
                    $"'{RateLimitOptions.SectionName}' needs a positive PreferenceLimit and a " +
                    "PreferenceWindow longer than zero.")
                // Checked while the host starts rather than on the first request, so a limit of
                // zero is a startup error naming the setting rather than an API that refuses
                // every registration and never says why.
                .ValidateOnStart();

            return services;
        }
    }
}
