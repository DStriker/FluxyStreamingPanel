using Fluxy.API.Contracts.Common;
using Fluxy.Core.Servers.Enums;
using Fluxy.Core.Servers.Outcomes;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts.Servers
{
    /// <summary>
    /// The one place a <see cref="ServerAction"/> becomes an HTTP status and a code.
    /// </summary>
    /// <remarks>
    /// The service knows nothing about HTTP and this class knows nothing about servers beyond
    /// which word maps to which answer. A duplicate answers 409 <c>server_already_exists</c> and
    /// the main row's deletion answers 409 <c>server_immutable</c>; both are refusals about a
    /// row rather than about the request, so the codes name the resource the way every other
    /// admin resource does.
    /// </remarks>
    public static class ServerResponses
    {
        /// <summary>Code reported once a server has been created.</summary>
        public const string CreatedCode = "server_created";

        /// <summary>Code reported once a server has been changed.</summary>
        public const string UpdatedCode = "server_updated";

        /// <summary>Code reported once a server has been deleted.</summary>
        public const string DeletedCode = "server_deleted";

        /// <summary>Code reported when no server answers to that identifier.</summary>
        public const string NotFoundCode = "server_not_found";

        /// <summary>Code reported when another server already holds that name.</summary>
        public const string AlreadyExistsCode = "server_already_exists";

        /// <summary>Code reported when the deletion addressed the main server.</summary>
        public const string ImmutableMainCode = "server_immutable";

        /// <summary>Answer describing <paramref name="outcome"/>.</summary>
        /// <param name="outcome">What the service reported.</param>
        /// <returns>The status and the body, with the rejected fields when the input was the problem.</returns>
        public static (int Status, MessageResponse Body) Describe(ServerOutcome outcome)
        {
            var (status, code, message) = outcome.Action switch
            {
                ServerAction.Created =>
                    (StatusCodes.Status201Created, CreatedCode, "The server has been created."),

                ServerAction.Updated =>
                    (StatusCodes.Status200OK, UpdatedCode, "The server has been updated."),

                ServerAction.Removed =>
                    (StatusCodes.Status200OK, DeletedCode, "The server has been deleted."),

                ServerAction.NotFound =>
                    (StatusCodes.Status404NotFound,
                        NotFoundCode,
                        "There is no server with that identifier."),

                ServerAction.AlreadyExists =>
                    (StatusCodes.Status409Conflict,
                        AlreadyExistsCode,
                        "Another server already has that name."),

                ServerAction.ImmutableMain =>
                    (StatusCodes.Status409Conflict,
                        ImmutableMainCode,
                        "The main server cannot be deleted."),

                ServerAction.Invalid =>
                    (StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Some of the values you entered are not valid."),

                // Unreachable: the service reports nothing outside this enum. Thrown rather
                // than answered as a 200, because a code a client has never seen with a status
                // it was never told to expect is a silent failure somewhere above this line.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome.Action,
                    "Unknown server outcome.")
            };

            return (status, new MessageResponse
            {
                Code = code,
                Message = message,
                Errors = FieldErrorKeys.FromPropertyNames(outcome.Errors)
            });
        }
    }
}
