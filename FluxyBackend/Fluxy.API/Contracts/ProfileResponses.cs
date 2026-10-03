using Fluxy.Core.Models.Users;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts
{
    /// <summary>
    /// Turns what a profile change reported into what a client is told: an HTTP status, a
    /// machine readable code and an English fallback.
    /// </summary>
    /// <remarks>
    /// The whole table lives in the transport layer, the same reason
    /// <see cref="RegistrationResponses"/> keeps one: the service knows only
    /// <see cref="ProfileChangeStatus"/> and never learns that one of its outcomes is a 409 and
    /// another a 403, so a status code can change without touching a rule.
    ///
    /// The codes are snake_case, matching the naming the protocol already uses, and they are the
    /// part of the contract a client branches on. Renaming one breaks every client; rewriting
    /// the wording beside it breaks nothing.
    /// </remarks>
    internal static class ProfileResponses
    {
        /// <summary>
        /// Code reported once a change has been applied - either because it needed no
        /// confirmation, or because its code was accepted.
        /// </summary>
        public const string UpdatedCode = "profile_updated";

        /// <summary>Code reported when a change is waiting for its code to be typed in.</summary>
        public const string SubmittedCode = "profile_change_submitted";

        /// <summary>Code reported once a change has been confirmed and is on the account.</summary>
        public const string ConfirmedCode = "profile_change_confirmed";

        /// <summary>Answer describing <paramref name="status"/>.</summary>
        /// <param name="status">Outcome reported by the service.</param>
        /// <param name="errors">
        /// Rejected fields, when there are any. Ignored for an outcome that is not about the
        /// input being wrong.
        /// </param>
        public static MessageResponse Describe(
            ProfileChangeStatus status,
            IReadOnlyDictionary<string, string[]>? errors = null)
        {
            var mapping = Map(status);

            return new MessageResponse
            {
                Code = mapping.Code,
                Message = mapping.Message,
                Errors = status is ProfileChangeStatus.InvalidInput ? errors : null
            };
        }

        /// <summary>HTTP status to answer <paramref name="status"/> with.</summary>
        public static int StatusCodeOf(ProfileChangeStatus status) => Map(status).StatusCode;

        /// <summary>
        /// The one place each outcome becomes a code, a status and a sentence.
        /// </summary>
        private static Mapping Map(ProfileChangeStatus status) => status switch
        {
            ProfileChangeStatus.Applied => new(
                StatusCodes.Status200OK,
                UpdatedCode,
                "Your profile has been updated."),

            ProfileChangeStatus.Submitted => new(
                StatusCodes.Status200OK,
                SubmittedCode,
                "Check your inbox for the confirmation code."),

            ProfileChangeStatus.Confirmed => new(
                StatusCodes.Status200OK,
                ConfirmedCode,
                "Your profile has been updated."),

            ProfileChangeStatus.InvalidCurrentPassword => new(
                StatusCodes.Status400BadRequest,
                "invalid_current_password",
                "The password you entered is not your current password."),

            ProfileChangeStatus.InvalidInput => new(
                StatusCodes.Status400BadRequest,
                "validation_failed",
                "Some of the values you entered are not valid."),

            ProfileChangeStatus.AlreadyExists => new(
                StatusCodes.Status409Conflict,
                "user_already_exists",
                "That username or email address is already taken."),

            ProfileChangeStatus.InvalidCode => new(
                StatusCodes.Status400BadRequest,
                "invalid_code",
                "The confirmation code is not valid."),

            ProfileChangeStatus.CodeExpired => new(
                StatusCodes.Status400BadRequest,
                "code_expired",
                "The confirmation code has expired. Request a new one."),

            ProfileChangeStatus.EmailDeliveryFailed => new(
                StatusCodes.Status502BadGateway,
                "email_delivery_failed",
                "The confirmation email could not be sent. Please try again later."),

            ProfileChangeStatus.AccountNotActive => new(
                StatusCodes.Status403Forbidden,
                "account_not_active",
                "This account cannot change its profile."),

            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

        /// <summary>The three values one outcome is rendered into.</summary>
        private readonly record struct Mapping(int StatusCode, string Code, string Message);
    }
}
