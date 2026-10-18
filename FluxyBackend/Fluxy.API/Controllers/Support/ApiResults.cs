using Fluxy.API.Contracts.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Support
{
    /// <summary>
    /// The refusals more than one controller answers with, built in one place.
    /// </summary>
    /// <remarks>
    /// A code is a contract: a client branches on <c>validation_failed</c> or
    /// <c>auth_required</c> whatever route produced it, so the body that carries one has to be
    /// the same body everywhere it appears. Refusals that are one controller's own - a
    /// throttle's answer, a status a service table maps - stay where they are made; what is
    /// here is the handful of answers the transport owes for the state of the request itself,
    /// before any rule has run.
    /// </remarks>
    internal static class ApiResults
    {
        /// <summary>
        /// The 400 a body or a query string broke a rule on, with the fields that broke it.
        /// </summary>
        /// <param name="errors">
        /// Rejected fields, keyed by the C# name of the property or by the camelCase name of a
        /// query parameter - both arrive in one shape after the rewrite.
        /// </param>
        public static ObjectResult ValidationFailed(Dictionary<string, string[]> errors)
            => new(new MessageResponse
            {
                Code = "validation_failed",
                Message = "Some of the values you entered are not valid.",
                Errors = FieldErrorKeys.FromPropertyNames(errors)
            })
            { StatusCode = StatusCodes.Status400BadRequest };

        /// <summary>
        /// The refusal for a token that carries no account identifier. <c>[Authorize]</c> has
        /// already said the request is authenticated, so a token without a subject is not a
        /// visitor who forgot to sign in - it is a token this application would not have minted,
        /// and answering about the session rather than about a header is the truthful reply.
        /// </summary>
        public static IActionResult Anonymous()
            => new ObjectResult(new MessageResponse
            {
                Code = "auth_required",
                Message = "Your session has ended. Please sign in again."
            })
            { StatusCode = StatusCodes.Status401Unauthorized };

        /// <summary>
        /// The refusal a blocked or missing account gets, with the sentence supplied by the
        /// caller.
        /// </summary>
        /// <remarks>
        /// The code is the one the profile change flow already answers with, so a client
        /// branching on <c>account_not_active</c> behaves the same way for an account that lost
        /// its standing on a read as it does on an edit. The sentence is a parameter because
        /// what the refusal is about differs by endpoint - a change that was refused and a page
        /// that was not handed over are not the same statement - and the caller is the one that
        /// knows which it is.
        /// </remarks>
        /// <param name="message">Sentence naming what the account could not do.</param>
        public static IActionResult AccountNotActive(string message)
            => new ObjectResult(new MessageResponse
            {
                Code = "account_not_active",
                Message = message
            })
            { StatusCode = StatusCodes.Status403Forbidden };
    }
}
