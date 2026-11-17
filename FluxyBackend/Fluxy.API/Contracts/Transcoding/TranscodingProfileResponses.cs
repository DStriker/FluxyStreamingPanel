using Fluxy.API.Contracts.Common;
using Fluxy.Core.Transcoding.Enums;
using Fluxy.Core.Transcoding.Outcomes;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts.Transcoding
{
    /// <summary>
    /// The one place a <see cref="TranscodingProfileAction"/> becomes an HTTP status and a code.
    /// </summary>
    /// <remarks>
    /// The service knows nothing about HTTP and this class knows nothing about profiles beyond
    /// which word maps to which answer. A duplicate answers 409 <c>transcoding_profile_already_exists</c>
    /// and a referenced profile answers 409 <c>transcoding_profile_in_use</c>; both are refusals
    /// about a row rather than about the request, so the codes name the resource the way every
    /// other admin resource does.
    /// </remarks>
    public static class TranscodingProfileResponses
    {
        /// <summary>Code reported once a profile has been created.</summary>
        public const string CreatedCode = "transcoding_profile_created";

        /// <summary>Code reported once a profile has been changed.</summary>
        public const string UpdatedCode = "transcoding_profile_updated";

        /// <summary>Code reported once a profile has been deleted.</summary>
        public const string DeletedCode = "transcoding_profile_deleted";

        /// <summary>Code reported when no profile answers to that identifier.</summary>
        public const string NotFoundCode = "transcoding_profile_not_found";

        /// <summary>Code reported when another profile already holds that name.</summary>
        public const string AlreadyExistsCode = "transcoding_profile_already_exists";

        /// <summary>Code reported when a stream, video or series still points at the profile.</summary>
        public const string InUseCode = "transcoding_profile_in_use";

        /// <summary>Answer describing <paramref name="outcome"/>.</summary>
        /// <param name="outcome">What the service reported.</param>
        /// <returns>The status and the body, with the rejected fields when the input was the problem.</returns>
        public static (int Status, MessageResponse Body) Describe(TranscodingProfileOutcome outcome)
        {
            var (status, code, message) = outcome.Action switch
            {
                TranscodingProfileAction.Created =>
                    (StatusCodes.Status201Created, CreatedCode, "The profile has been created."),

                TranscodingProfileAction.Updated =>
                    (StatusCodes.Status200OK, UpdatedCode, "The profile has been updated."),

                TranscodingProfileAction.Removed =>
                    (StatusCodes.Status200OK, DeletedCode, "The profile has been deleted."),

                TranscodingProfileAction.NotFound =>
                    (StatusCodes.Status404NotFound,
                        NotFoundCode,
                        "There is no transcoding profile with that identifier."),

                TranscodingProfileAction.AlreadyExists =>
                    (StatusCodes.Status409Conflict,
                        AlreadyExistsCode,
                        "Another profile already has that name."),

                TranscodingProfileAction.InUse =>
                    (StatusCodes.Status409Conflict,
                        InUseCode,
                        "The profile is still used by a stream, a video or a series."),

                TranscodingProfileAction.Invalid =>
                    (StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Some of the values you entered are not valid."),

                // Unreachable: the service reports nothing outside this enum. Thrown rather
                // than answered as a 200, because a code a client has never seen with a status
                // it was never told to expect is a silent failure somewhere above this line.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome.Action,
                    "Unknown transcoding profile outcome.")
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
