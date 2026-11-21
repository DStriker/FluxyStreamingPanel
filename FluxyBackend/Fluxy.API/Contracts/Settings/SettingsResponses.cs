using Fluxy.API.Contracts.Common;
using Fluxy.Core.Settings.Outcomes;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts.Settings
{
    /// <summary>
    /// The one place a <see cref="SettingsAction"/> becomes an HTTP status and a code.
    /// </summary>
    /// <remarks>
    /// The service knows nothing about HTTP and this class knows nothing about the settings
    /// beyond which word maps to which answer. There is no not-found and no conflict: the row is
    /// the installation's own and a write always ends either committed or refused field by field.
    /// </remarks>
    public static class SettingsResponses
    {
        /// <summary>Code reported once the application settings have been written.</summary>
        public const string UpdatedCode = "settings_updated";

        /// <summary>Answer describing <paramref name="outcome"/>.</summary>
        /// <param name="outcome">What the service reported.</param>
        /// <returns>The status and the body, with the rejected fields when the input was the problem.</returns>
        public static (int Status, MessageResponse Body) Describe(SettingsOutcome outcome)
        {
            var (status, code, message) = outcome.Action switch
            {
                SettingsAction.Updated =>
                    (StatusCodes.Status200OK,
                        UpdatedCode,
                        "The application settings have been updated."),

                SettingsAction.Invalid =>
                    (StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Some of the values you entered are not valid."),

                // Unreachable: the service reports nothing outside this enum. Thrown rather than
                // answered as a 200, because a code a client has never seen with a status it was
                // never told to expect is a silent failure somewhere above this line.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome.Action,
                    "Unknown settings outcome.")
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
