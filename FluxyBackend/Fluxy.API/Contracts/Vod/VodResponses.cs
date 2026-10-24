using Fluxy.API.Contracts.Common;
using Fluxy.Core.Vod.Enums;
using Fluxy.Core.Vod.Outcomes;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts.Vod
{
    /// <summary>
    /// The one place a <see cref="VodAction"/> becomes an HTTP status and a code.
    /// </summary>
    /// <remarks>
    /// The service knows nothing about HTTP and this class knows nothing about videos beyond
    /// which word maps to which answer. The codes follow the ones the streams, the groups and
    /// the categories already use, one level deeper, so a client that knows those sets knows
    /// the shape of this one.
    /// </remarks>
    public static class VodResponses
    {
        /// <summary>Code reported once a video has been created.</summary>
        public const string CreatedCode = "video_on_demand_created";

        /// <summary>Code reported once a video has been changed.</summary>
        public const string UpdatedCode = "video_on_demand_updated";

        /// <summary>Code reported once a video has been deleted.</summary>
        public const string DeletedCode = "video_on_demand_deleted";

        /// <summary>Code reported when no video answers to that identifier.</summary>
        public const string NotFoundCode = "video_on_demand_not_found";

        /// <summary>Code reported when another video of the same category holds that name.</summary>
        public const string AlreadyExistsCode = "video_on_demand_already_exists";

        /// <summary>Code reported when the source is neither a URL nor a readable file.</summary>
        public const string NotPlayableCode = "video_on_demand_not_playable";

        /// <summary>Code reported when a relay could not reach the source.</summary>
        public const string UnavailableCode = "video_on_demand_unavailable";

        /// <summary>Code reported when this installation has no transcoder.</summary>
        public const string TranscodeUnavailableCode = "transcode_not_configured";

        /// <summary>Answer describing <paramref name="outcome"/>.</summary>
        /// <param name="outcome">What the service reported.</param>
        /// <returns>The status, the body, and the rejected fields when the input was the problem.</returns>
        public static (int Status, MessageResponse Body) Describe(VodOutcome outcome)
        {
            var (status, code, message) = outcome.Action switch
            {
                VodAction.Created =>
                    (StatusCodes.Status201Created, CreatedCode, "The video has been created."),

                VodAction.Updated =>
                    (StatusCodes.Status200OK, UpdatedCode, "The video has been updated."),

                VodAction.Removed =>
                    (StatusCodes.Status200OK, DeletedCode, "The video has been deleted."),

                VodAction.NotFound =>
                    (StatusCodes.Status404NotFound,
                        NotFoundCode,
                        "There is no video with that identifier."),

                VodAction.AlreadyExists =>
                    (StatusCodes.Status409Conflict,
                        AlreadyExistsCode,
                        "That video name is already taken within this category."),

                VodAction.Invalid =>
                    (StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Some of the values you entered are not valid."),

                // Unreachable: the service reports nothing outside this enum. Thrown rather
                // than answered as a 200, because a code a client has never seen with a status
                // it was never told to expect is a silent failure somewhere above this line.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome.Action,
                    "Unknown on-demand outcome.")
            };

            return (status, new MessageResponse
            {
                Code = code,
                Message = message,
                Errors = FieldErrorKeys.FromPropertyNames(outcome.Errors)
            });
        }

        /// <summary>Answer describing a request to transcode one video.</summary>
        /// <param name="outcome">What the transcoding service reported.</param>
        public static (int Status, MessageResponse Body) Describe(VodTranscodeOutcome outcome)
        {
            var (status, code, message) = outcome.Action switch
            {
                VodTranscodeAction.Unavailable =>
                    (StatusCodes.Status503ServiceUnavailable,
                        TranscodeUnavailableCode,
                        "This server has no transcoder configured, so the video cannot be " +
                        "converted yet."),

                VodTranscodeAction.Invalid =>
                    (StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Some of the values you entered are not valid."),

                // Unreachable, for the same reason the switch above throws.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome.Action,
                    "Unknown transcoding outcome.")
            };

            return (status, new MessageResponse
            {
                Code = code,
                Message = message,
                Errors = FieldErrorKeys.FromPropertyNames(outcome.Errors)
            });
        }

        /// <summary>The refusal the play endpoint answers when a path names no readable file.</summary>
        public static MessageResponse NotPlayable() => new()
        {
            Code = NotPlayableCode,
            Message = "The source is neither an absolute URL nor a file this server can read."
        };

        /// <summary>The refusal the play endpoint answers when a relay cannot reach the source.</summary>
        public static MessageResponse Unavailable() => new()
        {
            Code = UnavailableCode,
            Message = "The source could not be reached."
        };
    }
}
