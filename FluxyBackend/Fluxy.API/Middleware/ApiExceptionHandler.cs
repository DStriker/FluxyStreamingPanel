using System.Text.Json;
using Fluxy.API.Contracts.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace Fluxy.API.Middleware
{
    /// <summary>
    /// Answers an unhandled exception with the same <c>{ code, message }</c> envelope every
    /// refusal of this API uses, instead of an empty body or the framework's default page.
    /// </summary>
    /// <remarks>
    /// The exception itself is logged with the request path and is never sent to the client.
    /// A stack trace in a response tells an attacker which library threw and where, and tells
    /// the visitor nothing they can act on; the code <c>server_error</c> is the part a client
    /// branches on.
    ///
    /// This handler is registered for every environment but only placed in the pipeline
    /// outside Development: a developer gets the exception page, which shows the stack trace
    /// and the source line that is the whole reason to run in Development.
    ///
    /// If the response has already started there is nothing left to replace, so the handler
    /// returns <c>false</c> and lets the server end the connection - writing a second body over
    /// the one already on the wire would corrupt the response instead of describing it.
    /// </remarks>
    public sealed class ApiExceptionHandler : IExceptionHandler
    {
        // Web defaults, because every controller response is camelCase and the frontend reads
        // `data.code` and `data.message`; the default serializer would send `Code` and `Message`
        // and the client would fall back to its own generic sentence.
        private static readonly JsonSerializerOptions BodyOptions = new(JsonSerializerDefaults.Web);

        private readonly ILogger<ApiExceptionHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiExceptionHandler"/> class.
        /// </summary>
        /// <param name="logger">Logger the exception is reported to.</param>
        public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            _logger.LogError(
                exception,
                "Unhandled exception while processing {Method} {Path}.",
                httpContext.Request.Method,
                httpContext.Request.Path);

            if (httpContext.Response.HasStarted)
            {
                return false;
            }

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            httpContext.Response.ContentType = "application/json; charset=utf-8";
            httpContext.Response.Headers.CacheControl = "no-store";

            await httpContext.Response.WriteAsync(
                JsonSerializer.Serialize(
                    new MessageResponse
                    {
                        Code = "server_error",
                        Message = "The server could not process the request. Please try again."
                    },
                    BodyOptions),
                cancellationToken);

            return true;
        }
    }
}
