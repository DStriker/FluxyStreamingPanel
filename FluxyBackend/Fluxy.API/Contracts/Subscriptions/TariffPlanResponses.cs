using Fluxy.API.Contracts.Common;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Outcomes;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts.Subscriptions
{
    /// <summary>
    /// The one place a <see cref="TariffPlanAction"/> becomes an HTTP status and a code.
    /// </summary>
    /// <remarks>
    /// The service knows nothing about HTTP and this class knows nothing about plans beyond
    /// which word maps to which answer. The codes follow the ones the bouquets and the guide
    /// sources already use, one level deeper, so a client that knows those sets knows the shape
    /// of this one.
    /// </remarks>
    public static class TariffPlanResponses
    {
        /// <summary>Code reported once a plan has been created.</summary>
        public const string CreatedCode = "tariff_plan_created";

        /// <summary>Code reported once a plan has been changed.</summary>
        public const string UpdatedCode = "tariff_plan_updated";

        /// <summary>Code reported once a plan has been deleted.</summary>
        public const string DeletedCode = "tariff_plan_deleted";

        /// <summary>Code reported when no plan answers to that identifier.</summary>
        public const string NotFoundCode = "tariff_plan_not_found";

        /// <summary>Code reported when another plan already holds that name.</summary>
        public const string AlreadyExistsCode = "tariff_plan_already_exists";

        /// <summary>Answer describing <paramref name="outcome"/>.</summary>
        /// <param name="outcome">What the service reported.</param>
        /// <returns>The status, the body, and the rejected fields when the input was the problem.</returns>
        public static (int Status, MessageResponse Body) Describe(TariffPlanOutcome outcome)
        {
            var (status, code, message) = outcome.Action switch
            {
                TariffPlanAction.Created =>
                    (StatusCodes.Status201Created, CreatedCode, "The tariff plan has been created."),

                TariffPlanAction.Updated =>
                    (StatusCodes.Status200OK, UpdatedCode, "The tariff plan has been updated."),

                TariffPlanAction.Deleted =>
                    (StatusCodes.Status200OK, DeletedCode, "The tariff plan has been deleted."),

                TariffPlanAction.NotFound =>
                    (StatusCodes.Status404NotFound,
                        NotFoundCode,
                        "There is no tariff plan with that identifier."),

                TariffPlanAction.AlreadyExists =>
                    (StatusCodes.Status409Conflict,
                        AlreadyExistsCode,
                        "That tariff plan name is already taken."),

                TariffPlanAction.Invalid =>
                    (StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Some of the values you entered are not valid."),

                // Unreachable: the service reports nothing outside this enum. Thrown rather
                // than answered as a 200, because a code a client has never seen with a status
                // it was never told to expect is a silent failure somewhere above this line.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome.Action,
                    "Unknown tariff plan outcome.")
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
