using Fluxy.Core.Models.Users;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts
{
    /// <summary>
    /// Turns what a registration call reported into what a client is told: an HTTP status, a
    /// machine readable code and an English fallback.
    /// </summary>
    /// <remarks>
    /// The whole table lives here, in the transport layer, and nowhere else. The service knows
    /// only <see cref="RegistrationStatus"/> and never learns that one of its outcomes is a 409
    /// and another a 502, which is what makes it possible to serve the same outcomes over a
    /// different transport later - or to change a status code without touching a rule.
    ///
    /// The codes are snake_case, matching the naming the protocol already uses for actions, and
    /// they are the part of the contract a client branches on. Renaming one breaks every client;
    /// rewriting the wording beside it breaks nothing.
    /// </remarks>
    internal static class RegistrationResponses
    {
        /// <summary>
        /// Answer describing <paramref name="status"/>.
        /// </summary>
        /// <param name="status">Outcome reported by the service.</param>
        /// <param name="errors">
        /// Rejected fields, when there are any. Ignored for an outcome that is not about the
        /// input being wrong.
        /// </param>
        public static MessageResponse Describe(
            RegistrationStatus status,
            IReadOnlyDictionary<string, string[]>? errors = null)
        {
            var mapping = Map(status);

            return new MessageResponse
            {
                Code = mapping.Code,
                Message = mapping.Message,
                Errors = status is RegistrationStatus.InvalidInput ? errors : null
            };
        }

        /// <summary>HTTP status to answer <paramref name="status"/> with.</summary>
        public static int StatusCodeOf(RegistrationStatus status) => Map(status).StatusCode;

        /// <summary>
        /// The one place each outcome becomes a code, a status and a sentence.
        /// </summary>
        private static Mapping Map(RegistrationStatus status) => status switch
        {
            RegistrationStatus.Submitted => new(
                StatusCodes.Status200OK,
                "registration_submitted",
                "Registration submitted. Check your inbox for the confirmation code."),

            RegistrationStatus.RegistrationNotConfigured => new(
                StatusCodes.Status503ServiceUnavailable,
                "registration_not_configured",
                "Registration is not configured on this server."),

            RegistrationStatus.InvalidInput => new(
                StatusCodes.Status400BadRequest,
                "validation_failed",
                "Some of the values you entered are not valid."),

            RegistrationStatus.AlreadyExists => new(
                StatusCodes.Status409Conflict,
                "user_already_exists",
                "That username or email address is already taken."),

            RegistrationStatus.Confirmed => new(
                StatusCodes.Status200OK,
                "registration_confirmed",
                "Registration confirmed. You can sign in now."),

            RegistrationStatus.InvalidCode => new(
                StatusCodes.Status400BadRequest,
                "invalid_code",
                "The confirmation code is not valid."),

            RegistrationStatus.CodeExpired => new(
                StatusCodes.Status400BadRequest,
                "code_expired",
                "The confirmation code has expired. Request a new one."),

            RegistrationStatus.EmailDeliveryFailed => new(
                StatusCodes.Status502BadGateway,
                "email_delivery_failed",
                "The confirmation email could not be sent. Please try again later."),

            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

        /// <summary>The three values one outcome is rendered into.</summary>
        private readonly record struct Mapping(int StatusCode, string Code, string Message);
    }
}