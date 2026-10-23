using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Outcomes;
using Fluxy.API.Contracts.Common;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts.Streams
{
    /// <summary>
    /// The one place an <see cref="AudioStreamAction"/> becomes an HTTP status and a code.
    /// </summary>
    /// <remarks>
    /// The service knows nothing about HTTP and this class knows nothing about streams beyond
    /// which word maps to which answer, which is the whole reason both exist: a rule can change
    /// without anybody having to decide what status code it should start returning, and a
    /// status can move without touching a rule.
    /// </remarks>
    public static class AudioStreamResponses
    {
        /// <summary>Code reported once a stream has been created.</summary>
        public const string CreatedCode = "audio_stream_created";

        /// <summary>Code reported once a stream has been changed.</summary>
        public const string UpdatedCode = "audio_stream_updated";

        /// <summary>Code reported once a stream has been deleted.</summary>
        public const string DeletedCode = "audio_stream_deleted";

        /// <summary>Code reported once a stream has been started.</summary>
        public const string StartedCode = "audio_stream_started";

        /// <summary>Code reported once a stream has been stopped.</summary>
        public const string StoppedCode = "audio_stream_stopped";

        /// <summary>Code reported once a stream has been started again.</summary>
        public const string RestartedCode = "audio_stream_restarted";

        /// <summary>Code reported when no stream answers to that identifier.</summary>
        public const string NotFoundCode = "audio_stream_not_found";

        /// <summary>Code reported when another stream of the same category holds that name.</summary>
        public const string AlreadyExistsCode = "audio_stream_already_exists";

        /// <summary>Answer describing <paramref name="outcome"/>.</summary>
        /// <param name="outcome">What the service reported.</param>
        /// <returns>The status, the body, and the rejected fields when the input was the problem.</returns>
        public static (int Status, MessageResponse Body) Describe(AudioStreamOutcome outcome)
        {
            var (status, code, message) = outcome.Action switch
            {
                AudioStreamAction.Created =>
                    (StatusCodes.Status201Created, CreatedCode, "The stream has been created."),

                AudioStreamAction.Updated =>
                    (StatusCodes.Status200OK, UpdatedCode, "The stream has been updated."),

                AudioStreamAction.Removed =>
                    (StatusCodes.Status200OK, DeletedCode, "The stream has been deleted."),

                // A start and a stop are answers about the row rather than about a process,
                // and starting a stream that is already running is a success rather than a
                // complaint.
                AudioStreamAction.Started =>
                    (StatusCodes.Status200OK, StartedCode, "The stream has been started."),

                AudioStreamAction.Stopped =>
                    (StatusCodes.Status200OK, StoppedCode, "The stream has been stopped."),

                AudioStreamAction.Restarted =>
                    (StatusCodes.Status200OK, RestartedCode, "The stream has been restarted."),

                AudioStreamAction.NotFound =>
                    (StatusCodes.Status404NotFound,
                        NotFoundCode,
                        "There is no stream with that identifier."),

                AudioStreamAction.AlreadyExists =>
                    (StatusCodes.Status409Conflict,
                        AlreadyExistsCode,
                        "That stream name is already taken within this category."),

                AudioStreamAction.Invalid =>
                    (StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Some of the values you entered are not valid."),

                // Unreachable: the service reports nothing outside this enum. Thrown rather
                // than answered as a 200, because a code a client has never seen with a
                // status it was never told to expect is a silent failure somewhere above
                // this line.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome.Action,
                    "Unknown audio stream outcome.")
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
