using Fluxy.API.Contracts.Common;
using Fluxy.Core.Settings.Outcomes;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts.Settings
{
    /// <summary>
    /// The one place a <see cref="MagSettingsAction"/> becomes an HTTP status and a code.
    /// </summary>
    /// <remarks>
    /// The shape is <see cref="SettingsResponses"/>'s, stated as its own table because the two
    /// resources answer their own codes and a client branches on which screen it saved.
    /// </remarks>
    public static class MagSettingsResponses
    {
        /// <summary>Code reported once the MAG settings have been written.</summary>
        public const string UpdatedCode = "mag_settings_updated";

        /// <summary>Answer describing <paramref name="outcome"/>.</summary>
        /// <param name="outcome">What the service reported.</param>
        /// <returns>The status and the body, with the rejected fields when the input was the problem.</returns>
        public static (int Status, MessageResponse Body) Describe(MagSettingsOutcome outcome)
        {
            var (status, code, message) = outcome.Action switch
            {
                MagSettingsAction.Updated =>
                    (StatusCodes.Status200OK,
                        UpdatedCode,
                        "The MAG settings have been updated."),

                MagSettingsAction.Invalid =>
                    (StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Some of the values you entered are not valid."),

                // Unreachable: the service reports nothing outside this enum. Thrown rather than
                // answered as a 200, for the reason the application settings table gives.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome.Action,
                    "Unknown MAG settings outcome.")
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
