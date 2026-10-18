using System.Text.Json;
using Fluxy.API.Contracts.Common;
using Fluxy.API.Middleware;

namespace Fluxy.API.Extensions
{
    /// <summary>
    /// The two halves of the error envelope that sit outside MVC: unhandled exceptions and the
    /// statuses routing produces before any action runs.
    /// </summary>
    /// <remarks>
    /// Every controller answers with <c>{ code, message }</c> through <c>ApiResults</c>, but the
    /// framework has two paths that never reach an action: an exception, and a 404/405/415
    /// decided by routing. Those used to answer with an empty body, which the frontend can only
    /// render as its generic "something went wrong" - the same bodyless answer a network failure
    /// gets, with no way to tell "this address does not exist" from "the server broke".
    ///
    /// The status-code half is filtered by status on purpose. The authorization handler already
    /// writes <c>auth_required</c> and <c>permission_denied</c> bodies, and every action writes
    /// its own; <c>UseStatusCodePages</c> only touches a response that has no body at all, and
    /// the explicit 404/405/415 list keeps it from ever re-dressing a refusal that already
    /// means something else.
    /// </remarks>
    public static class ExceptionHandlingExtensions
    {
        // Web defaults for the same reason as ApiExceptionHandler: the client reads camelCase.
        private static readonly JsonSerializerOptions BodyOptions = new(JsonSerializerDefaults.Web);

        /// <summary>
        /// Registers the handler that turns an unhandled exception into the API envelope.
        /// </summary>
        /// <param name="services">Service collection to add the handler to.</param>
        /// <returns>The same collection, so registrations can be chained.</returns>
        public static IServiceCollection AddFluxyExceptionHandling(this IServiceCollection services)
        {
            services.AddExceptionHandler<ApiExceptionHandler>();

            // UseExceptionHandler() without a path or a delegate still insists on a problem
            // details service, even though ApiExceptionHandler answers every exception it can.
            // This is that dependency, not a fallback choice: the handler returns true first.
            services.AddProblemDetails();

            return services;
        }

        /// <summary>
        /// Places the exception handler in the pipeline. Call it before routing, so it wraps
        /// every endpoint and every piece of middleware that may throw.
        /// </summary>
        /// <param name="app">Application builder to extend.</param>
        /// <returns>The same builder, so the pipeline can be chained.</returns>
        public static IApplicationBuilder UseFluxyExceptionHandling(this IApplicationBuilder app)
        {
            app.UseExceptionHandler();

            return app;
        }

        /// <summary>
        /// Gives the bodyless statuses routing produces the same body as every other refusal.
        /// </summary>
        /// <param name="app">Application builder to extend.</param>
        /// <returns>The same builder, so the pipeline can be chained.</returns>
        /// <remarks>
        /// Only 404, 405 and 415 are described. A 401 or 403 that reaches here has no body for a
        /// reason this method cannot know, and inventing one would hide the difference between
        /// "sign in again" and "you may not do this"; those keep whatever the authorization
        /// middleware decided. A 400 from the framework already carries model errors through the
        /// custom model-state factory.
        /// </remarks>
        public static IApplicationBuilder UseFluxyStatusCodePages(this IApplicationBuilder app)
        {
            app.UseStatusCodePages(async context =>
            {
                var response = context.HttpContext.Response;

                var (code, message) = response.StatusCode switch
                {
                    StatusCodes.Status404NotFound => (
                        "not_found",
                        "The requested resource was not found."),
                    StatusCodes.Status405MethodNotAllowed => (
                        "method_not_allowed",
                        "The method is not allowed for this resource."),
                    StatusCodes.Status415UnsupportedMediaType => (
                        "unsupported_media_type",
                        "The request body has a media type this API does not support."),
                    _ => (string.Empty, string.Empty)
                };

                if (code.Length == 0)
                {
                    return;
                }

                response.ContentType = "application/json; charset=utf-8";
                response.Headers.CacheControl = "no-store";

                await response.WriteAsync(
                    JsonSerializer.Serialize(
                        new MessageResponse
                        {
                            Code = code,
                            Message = message
                        },
                        BodyOptions));
            });

            return app;
        }
    }
}
