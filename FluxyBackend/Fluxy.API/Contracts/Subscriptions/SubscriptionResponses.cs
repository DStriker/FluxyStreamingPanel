using Fluxy.API.Contracts.Common;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Outcomes;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts.Subscriptions
{
    /// <summary>
    /// The one place a <see cref="SubscriptionAction"/> becomes an HTTP status and a code.
    /// </summary>
    /// <remarks>
    /// The service knows nothing about HTTP and this class knows nothing about subscriptions
    /// beyond which word maps to which answer. The codes follow the ones the plans and the
    /// bouquets already use, one level deeper, so a client that knows those sets knows the shape
    /// of this one.
    /// </remarks>
    public static class SubscriptionResponses
    {
        /// <summary>Code reported once a subscription has been issued.</summary>
        public const string CreatedCode = "subscription_created";

        /// <summary>Code reported once a subscription has been changed.</summary>
        public const string UpdatedCode = "subscription_updated";

        /// <summary>Code reported once a subscription has been deleted.</summary>
        public const string DeletedCode = "subscription_deleted";

        /// <summary>Code reported when no subscription answers to that identifier.</summary>
        public const string NotFoundCode = "subscription_not_found";

        /// <summary>Code reported when another subscription holds the playlist id or MAC.</summary>
        public const string AlreadyExistsCode = "subscription_already_exists";

        /// <summary>Code reported when a plan change cleared a service the new plan cannot deliver.</summary>
        public const string PlaylistClearedCode = "subscription_playlist_cleared";

        /// <summary>Code reported when a plan change cleared a MAG MAC the new plan cannot deliver.</summary>
        public const string MagClearedCode = "subscription_mag_cleared";

        /// <summary>Code reported when a plan change cleared an Enigma2 MAC the new plan cannot deliver.</summary>
        public const string Enigma2ClearedCode = "subscription_enigma2_cleared";

        /// <summary>Answer describing <paramref name="outcome"/>.</summary>
        /// <param name="outcome">What the service reported.</param>
        /// <returns>The status, the body, and the rejected fields when the input was the problem.</returns>
        public static (int Status, MessageResponse Body) Describe(SubscriptionOutcome outcome)
        {
            var (status, code, message) = outcome.Action switch
            {
                SubscriptionAction.Created =>
                    (StatusCodes.Status201Created, CreatedCode, "The subscription has been created."),

                SubscriptionAction.Updated =>
                    (StatusCodes.Status200OK, UpdatedCode, "The subscription has been updated."),

                SubscriptionAction.Deleted =>
                    (StatusCodes.Status200OK, DeletedCode, "The subscription has been deleted."),

                SubscriptionAction.NotFound =>
                    (StatusCodes.Status404NotFound,
                        NotFoundCode,
                        "There is no subscription with that identifier."),

                SubscriptionAction.AlreadyExists =>
                    (StatusCodes.Status409Conflict,
                        AlreadyExistsCode,
                        "Another subscription already holds that playlist id or device address."),

                SubscriptionAction.Invalid =>
                    (StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Some of the values you entered are not valid."),

                // Unreachable: the service reports nothing outside this enum. Thrown rather than
                // answered as a 200, because a code a client has never seen with a status it was
                // never told to expect is a silent failure somewhere above this line.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome.Action,
                    "Unknown subscription outcome.")
            };

            return (status, new MessageResponse
            {
                Code = code,
                Message = message,
                Errors = FieldErrorKeys.FromPropertyNames(outcome.Errors)
            });
        }

        /// <summary>The warning a bulk run reports for one service a plan change cleared.</summary>
        /// <param name="warning">The clearance the service reported.</param>
        /// <returns>The code and the English fallback sentence.</returns>
        public static (string Code, string Message) DescribeWarning(SubscriptionBulkWarning warning)
            => warning switch
            {
                SubscriptionBulkWarning.PlaylistCleared =>
                    (PlaylistClearedCode,
                        "The new tariff plan does not deliver a playlist, so it was cleared."),

                SubscriptionBulkWarning.MagCleared =>
                    (MagClearedCode,
                        "The new tariff plan does not support MAG devices, so the MAG was cleared."),

                SubscriptionBulkWarning.Enigma2Cleared =>
                    (Enigma2ClearedCode,
                        "The new tariff plan does not support Enigma2 devices, so the Enigma2 " +
                        "device was cleared."),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(warning),
                    warning,
                    "Unknown subscription bulk warning.")
            };
    }
}
