using Fluxy.Core.Models.Users;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts
{
    /// <summary>
    /// Turns what a password reset call reported into what a client is told: an HTTP status, a
    /// machine readable code and an English fallback.
    /// </summary>
    /// <remarks>
    /// The same arrangement <see cref="RegistrationResponses"/> uses, for the same reason: the
    /// service reports <see cref="PasswordResetStatus"/> and never sees a status code.
    ///
    /// One choice is worth stating because it is the opposite of the registration endpoint's.
    /// Registration answers every unknown address with one code, so the form cannot be used to
    /// discover accounts. A reset answers a username and an email that do not belong together
    /// with <c>credentials_mismatch</c> - a refusal the visitor needs, because the alternative
    /// is waiting for a message that will never arrive with no idea which half was wrong. It is
    /// a code of its own rather than the sign-in's <c>invalid_credentials</c>, which says "the
    /// password is wrong" and would be a lie on a form that has no password yet. That
    /// makes the pair probeable, and the cost is paid by counting every attempt against the
    /// same window a sign-in gets, rather than by telling the visitor nothing.
    /// </remarks>
    internal static class PasswordResetResponses
    {
        /// <summary>Code reported when the code is on its way to the address.</summary>
        public const string SubmittedCode = "password_reset_submitted";

        /// <summary>Code reported once the new password is on the account.</summary>
        public const string ConfirmedCode = "password_reset_confirmed";

        /// <summary>Answer describing <paramref name="status"/>.</summary>
        /// <param name="status">Outcome reported by the service.</param>
        /// <param name="errors">
        /// Rejected fields, when there are any. Ignored for an outcome that is not about the
        /// input being wrong.
        /// </param>
        public static MessageResponse Describe(
            PasswordResetStatus status,
            IReadOnlyDictionary<string, string[]>? errors = null)
        {
            var mapping = Map(status);

            return new MessageResponse
            {
                Code = mapping.Code,
                Message = mapping.Message,
                Errors = status is PasswordResetStatus.InvalidInput ? errors : null
            };
        }

        /// <summary>HTTP status to answer <paramref name="status"/> with.</summary>
        public static int StatusCodeOf(PasswordResetStatus status) => Map(status).StatusCode;

        /// <summary>
        /// The one place each outcome becomes a code, a status and a sentence.
        /// </summary>
        private static Mapping Map(PasswordResetStatus status) => status switch
        {
            PasswordResetStatus.Submitted => new(
                StatusCodes.Status200OK,
                SubmittedCode,
                "Check your inbox for the confirmation code."),

            PasswordResetStatus.NotConfigured => new(
                StatusCodes.Status503ServiceUnavailable,
                "password_reset_not_configured",
                "Password reset is not configured on this server."),

            PasswordResetStatus.InvalidPair => new(
                StatusCodes.Status400BadRequest,
                "credentials_mismatch",
                "That username and email address do not belong to the same account."),

            PasswordResetStatus.InvalidInput => new(
                StatusCodes.Status400BadRequest,
                "validation_failed",
                "Some of the values you entered are not valid."),

            PasswordResetStatus.EmailDeliveryFailed => new(
                StatusCodes.Status502BadGateway,
                "email_delivery_failed",
                "The confirmation email could not be sent. Please try again later."),

            PasswordResetStatus.InvalidCode => new(
                StatusCodes.Status400BadRequest,
                "invalid_code",
                "The confirmation code is not valid."),

            PasswordResetStatus.CodeExpired => new(
                StatusCodes.Status400BadRequest,
                "code_expired",
                "The confirmation code has expired. Request a new one."),

            PasswordResetStatus.Confirmed => new(
                StatusCodes.Status200OK,
                ConfirmedCode,
                "Your password has been changed. You can sign in now."),

            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

        /// <summary>The three values one outcome is rendered into.</summary>
        private readonly record struct Mapping(int StatusCode, string Code, string Message);
    }
}
