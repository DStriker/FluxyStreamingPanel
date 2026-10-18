using Fluxy.API.Contracts.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxy.API.Extensions
{
    /// <summary>
    /// Wiring of the answer a body that failed model validation is given.
    /// </summary>
    /// <remarks>
    /// The call belongs after <c>AddControllers</c>, and that placement is the whole reason this
    /// lives in its own method rather than being inlined somewhere that "looks neater". MVC
    /// registers its own default factory for <see cref="ApiBehaviorOptions"/> through the same
    /// options system, and configuration actions run in the order they were registered, so a
    /// call placed before the controllers would be overwritten by the very default it exists to
    /// replace.
    /// </remarks>
    public static class ApiBehaviorExtensions
    {
        /// <summary>
        /// Replaces the default rejection body with this API's own error envelope.
        /// </summary>
        /// <param name="services">Service collection to configure the options in.</param>
        /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
        public static IServiceCollection AddFluxyApiBehavior(this IServiceCollection services)
        {
            // There is no AddApiBehaviorOptions in this framework version; the options object is
            // configured through the ordinary options system, which MVC picks up.
            services.Configure<ApiBehaviorOptions>(ConfigureApiBehavior);

            return services;
        }

        /// <summary>
        /// Replaces the shape a rejected body is answered with.
        /// </summary>
        /// <remarks>
        /// The default is a <c>ValidationProblemDetails</c>, which carries the offending fields but
        /// no <c>message</c>. The frontend reads <c>data.message</c> and nothing else, so the
        /// default answer reaches it as an empty string and the visitor sees "something went
        /// wrong" instead of what was wrong with the form. This produces the same body as every
        /// other failure of this API, with the fields added on top.
        /// </remarks>
        private static void ConfigureApiBehavior(ApiBehaviorOptions options)
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                // Grouped rather than turned into a dictionary directly, because a value that
                // failed both while being read and while being validated lands under two keys
                // that normalize to the same name - and a duplicate key would throw out of a
                // factory that is supposed to be describing a problem, not causing one.
                var errors = context.ModelState
                    .Where(entry => entry.Value is { Errors.Count: > 0 })
                    .GroupBy(entry => FieldErrorKeys.FromModelStateKey(entry.Key))
                    .ToDictionary(
                        group => group.Key,
                        group => group
                            .SelectMany(entry => entry.Value!.Errors)
                            .Select(Describe)
                            .ToArray());

                return new BadRequestObjectResult(new MessageResponse
                {
                    Code = "validation_failed",
                    Message = "Some of the values you entered are not valid.",
                    Errors = errors.Count == 0 ? null : errors
                });
            };
        }

        /// <summary>
        /// Text for one rejected value. A binding failure without a message of its own - a value
        /// that could not be read as the type it was declared as - gets a generic one rather than
        /// an empty string, so no entry in the map is ever blank.
        /// </summary>
        private static string Describe(ModelError error)
            => string.IsNullOrWhiteSpace(error.ErrorMessage)
                ? "The value is not valid."
                : error.ErrorMessage;
    }
}
