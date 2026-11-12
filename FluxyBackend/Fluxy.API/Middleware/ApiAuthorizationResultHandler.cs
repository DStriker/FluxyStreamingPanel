using System.Security.Claims;
using System.Text.Json;
using Fluxy.API.Contracts.Authentication;
using Fluxy.API.Contracts.Common;
using Fluxy.API.Security;
using Fluxy.Core.Authentication.Options;
using Fluxy.Core.Authentication.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Fluxy.API.Middleware
{
    /// <summary>
    /// Turns the framework's authorization failures into the body every other failure of this
    /// API already uses.
    /// </summary>
    /// <remarks>
    /// The default answer is a bare status with a <c>WWW-Authenticate</c> header and no body at
    /// all. That is a reasonable shape for an HTTP API and the wrong one here, for two reasons.
    /// The frontend reads <c>data.message</c> and nothing else, so an empty body reaches it as
    /// an empty string and the visitor is told nothing about what happened. And the header is
    /// an instruction to a browser to show its own credentials prompt, which would replace a
    /// page of this application with a dialog that cannot complete - the browser would collect a
    /// password and have nowhere to send it, because this API has no login form of its own.
    ///
    /// So the header is withheld and the body is the documented shape, carrying the code the
    /// client branches on and the path it should send the visitor to.
    /// </remarks>
    public sealed class ApiAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
    {
        private const string HeaderName = "X-Redirect-To";
        private const string RequestInfoKey = "ApiAuthorizationResultHandler.Handled";

        // Web defaults, because every controller response goes through MVC's formatter, which
        // serializes with camelCase, and that is the shape the client reads (`data.code`,
        // `data.message`, `data.redirect`). A bare `JsonSerializer.Serialize` uses the default
        // PascalCase policy, so `Code` would arrive as `undefined`, the frontend would fall
        // back to its generic `server_error`, and the code this class exists to deliver - the
        // difference between "sign in again" and "you are not allowed here" - would be lost.
        private static readonly JsonSerializerOptions BodyOptions = new(JsonSerializerDefaults.Web);

        private readonly AuthorizationMiddlewareResultHandler _default = new();
        private readonly IOptionsMonitor<AuthenticationOptions> _options;
        private readonly ILogger<ApiAuthorizationResultHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiAuthorizationResultHandler"/> class.
        /// </summary>
        /// <param name="options">Live settings, so a configuration reload applies without a restart.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public ApiAuthorizationResultHandler(
            IOptionsMonitor<AuthenticationOptions> options,
            ILogger<ApiAuthorizationResultHandler> logger)
        {
            _options = options;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task HandleAsync(
            RequestDelegate next,
            HttpContext context,
            AuthorizationPolicy policy,
            PolicyAuthorizationResult authorizeResult)
        {
            if (authorizeResult.Succeeded)
            {
                await _default.HandleAsync(next, context, policy, authorizeResult);

                return;
            }

            // Two outcomes, and they are told apart on purpose. <c>Challenged</c> means the
            // request carried no token that the server accepts - the visitor is not signed in,
            // or their token expired. <c>Forbidden</c> means they are signed in and the account
            // behind the token may not do this. A client that conflates them would show "please
            // sign in" to somebody who is already signed in, which is both wrong and a hint about
            // what they are allowed to see.
            if (authorizeResult.Challenged)
            {
                await WriteAsync(
                    context,
                    StatusCodes.Status401Unauthorized,
                    AuthenticationResponses.AuthenticationRequiredCode,
                    "Your session has ended. Please sign in again.",
                    clientLoginPath: true);

                return;
            }

            var userId = context.User.FindFirst(TokenClaimTypes.Subject)?.Value
                ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var code = await ExplainAsync(context, policy);

            _logger.LogInformation(
                "Refused a request from {Path} to account {UserId}: the token is valid but does not " +
                "grant this endpoint ({Code}).",
                context.Request.Path,
                userId ?? "an unidentified account",
                code);

            // A permission refusal does not send anybody to the sign-in page. The visitor is
            // signed in and signing in again would change nothing - what they are missing is a
            // grant on their group, and the page they asked for is where that message belongs.
            // The role refusal keeps the redirect, because it is the one that may mean the
            // session belongs to an account whose area has moved out from under it.
            await WriteAsync(
                context,
                StatusCodes.Status403Forbidden,
                code,
                code == AuthenticationResponses.PermissionDeniedCode
                    ? "Your account does not have permission for this."
                    : "Your account does not have access to this area.",
                clientLoginPath: code != AuthenticationResponses.PermissionDeniedCode);
        }

        /// <summary>
        /// Which of the two refusals this one is, re-derived from the policy and the account.
        /// </summary>
        /// <remarks>
        /// Re-derived rather than reported by the handlers because the framework hands this
        /// class nothing but "it failed" - a <c>PolicyAuthorizationResult</c> carries no list of
        /// which requirements went unsatisfied, and neither does the authorization context by
        /// the time it gets here. So the question is asked again of the account, on a path that
        /// only runs when something was already refused - and the reader it asks is the same
        /// scoped one both handlers used, so this costs no read at all.
        ///
        /// Every branch that is not a missing permission answers
        /// <see cref="AuthenticationResponses.RoleChangedCode"/>, which is the refusal this API
        /// gave before permissions existed - a wrong level, an inactive account, a stale token
        /// claim. Those are all the same thing to a client: this account is not welcome here.
        /// </remarks>
        /// <param name="context">Request being refused.</param>
        /// <param name="policy">Policy whose requirements did not succeed.</param>
        /// <returns>The code the body carries.</returns>
        private async Task<string> ExplainAsync(HttpContext context, AuthorizationPolicy policy)
        {
            // Every permission requirement is read, not the first one. A policy built from a
            // single attribute carries one, but stacked [Authorize] attributes merge into one
            // policy that carries one requirement per attribute - and reading only the first
            // would answer "your role changed" for an account whose role is fine and whose
            // group is simply missing the second, third or fourth grant.
            var demanded = policy.Requirements
                .OfType<PermissionRequirement>()
                .ToArray();

            if (demanded.Length == 0)
            {
                return AuthenticationResponses.RoleChangedCode;
            }

            if (await context.RequestServices
                    .GetRequiredService<RequestStandingReader>()
                    .ReadAsync(context.User) is not { } found)
            {
                return AuthenticationResponses.RoleChangedCode;
            }

            var (userId, standing) = found;

            if (!standing.IsActive)
            {
                return AuthenticationResponses.RoleChangedCode;
            }

            var roles = policy.Requirements
                .OfType<RoleRequirement>()
                .ToArray();

            if (roles.Length > 0 && roles.Any(role => standing.Role != role.Role))
            {
                return AuthenticationResponses.RoleChangedCode;
            }

            // A permission of a group the account is not in any more reaches this line, and so
            // does a grant withdrawn while the token was still good. Both are the answer the
            // reader is after; neither is the same as "you are in the wrong area", which is why
            // the two codes are different.
            //
            // The rule is all-of rather than any-of because that is the semantics of stacked
            // [Authorize] attributes: the policy merges their requirements and every one of
            // them must succeed. An endpoint demanding four grants therefore answers
            // permission_denied unless the group holds all four, and the client can put the
            // refusal where it belongs - on the screen, not on a sign-in page.
            return demanded.All(requirement => requirement.Permissions.Any(standing.Grants))
                ? AuthenticationResponses.RoleChangedCode
                : AuthenticationResponses.PermissionDeniedCode;
        }

        private async Task WriteAsync(
            HttpContext context,
            int statusCode,
            string code,
            string message,
            bool clientLoginPath)
        {
            var path = clientLoginPath
                ? _options.CurrentValue.ClientLoginPath
                : null;

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";

            if (path is not null)
            {
                // Also in a header, so a client that reads only headers still has somewhere to
                // send the visitor. The header is a hint and not the contract; the body field is.
                context.Response.Headers[HeaderName] = path;
            }

            await context.Response.WriteAsync(JsonSerializer.Serialize(
                new MessageResponse
                {
                    Code = code,
                    Message = message,
                    Redirect = path is null ? null : "/" + path.TrimStart('/')
                },
                BodyOptions));
        }
    }
}
