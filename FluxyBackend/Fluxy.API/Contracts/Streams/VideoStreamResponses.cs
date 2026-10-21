using Fluxy.Core.Streams.Enums;
using Fluxy.API.Contracts.Common;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts.Streams
{
    /// <summary>
    /// The one place a <see cref="VideoStreamAction"/> becomes an HTTP status and a code.
    /// </summary>
    /// <remarks>
    /// The service knows nothing about HTTP and this class knows nothing about streams beyond
    /// which word maps to which answer, which is the whole reason both exist: a rule can change
    /// without anybody having to decide what status code it should start returning, and a
    /// status can move without touching a rule.
    ///
    /// The codes follow the ones the accounts, the groups and the categories already use -
    /// <c>user_blocked</c>, <c>user_group_in_use</c>, <c>content_category_already_exists</c> -
    /// one level deeper, so a client that knows those sets knows the shape of this one without
    /// reading a fourth table of exceptions.
    /// </remarks>
    public static class VideoStreamResponses
    {
        /// <summary>Code reported once a stream has been created.</summary>
        public const string CreatedCode = "video_stream_created";

        /// <summary>Code reported once a stream has been changed.</summary>
        public const string UpdatedCode = "video_stream_updated";

        /// <summary>Code reported once a stream has been deleted.</summary>
        public const string DeletedCode = "video_stream_deleted";

        /// <summary>Code reported once a stream has been started.</summary>
        public const string StartedCode = "video_stream_started";

        /// <summary>Code reported once a stream has been stopped.</summary>
        public const string StoppedCode = "video_stream_stopped";

        /// <summary>Code reported once a stream has been started again.</summary>
        public const string RestartedCode = "video_stream_restarted";

        /// <summary>Code reported when a bulk type change cleared a stream's delay.</summary>
        public const string DelayClearedWarningCode = "video_stream_delay_cleared";

        /// <summary>Code reported when a bulk type change cleared a stream's archive length.</summary>
        public const string ArchiveClearedWarningCode = "video_stream_archive_cleared";

        /// <summary>Code reported when a bulk type change cleared the restream settings.</summary>
        public const string RestreamClearedWarningCode = "video_stream_restream_cleared";

        /// <summary>Code reported when no stream answers to that identifier.</summary>
        public const string NotFoundCode = "video_stream_not_found";

        /// <summary>Code reported when another stream of the same category holds that name.</summary>
        public const string AlreadyExistsCode = "video_stream_already_exists";

        /// <summary>Answer describing <paramref name="outcome"/>.</summary>
        /// <param name="outcome">What the service reported.</param>
        /// <returns>The status, the body, and the rejected fields when the input was the problem.</returns>
        public static (int Status, MessageResponse Body) Describe(VideoStreamOutcome outcome)
        {
            var (status, code, message) = outcome.Action switch
            {
                VideoStreamAction.Created =>
                    (StatusCodes.Status201Created, CreatedCode, "The stream has been created."),

                VideoStreamAction.Updated =>
                    (StatusCodes.Status200OK, UpdatedCode, "The stream has been updated."),

                VideoStreamAction.Removed =>
                    (StatusCodes.Status200OK, DeletedCode, "The stream has been deleted."),

                // A start and a stop are answers about the row rather than about a process,
                // and starting a stream that is already running is a success rather than a
                // complaint - the row says what the operator asked it to say, and refusing
                // would make a double click look like a failure of the thing the visitor was
                // trying to achieve.
                VideoStreamAction.Started =>
                    (StatusCodes.Status200OK, StartedCode, "The stream has been started."),

                VideoStreamAction.Stopped =>
                    (StatusCodes.Status200OK, StoppedCode, "The stream has been stopped."),

                VideoStreamAction.Restarted =>
                    (StatusCodes.Status200OK, RestartedCode, "The stream has been restarted."),

                VideoStreamAction.NotFound =>
                    (StatusCodes.Status404NotFound,
                        NotFoundCode,
                        "There is no stream with that identifier."),

                VideoStreamAction.AlreadyExists =>
                    (StatusCodes.Status409Conflict,
                        AlreadyExistsCode,
                        "That stream name is already taken within this category."),

                VideoStreamAction.Invalid =>
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
                    "Unknown video stream outcome.")
            };

            return (status, new MessageResponse
            {
                Code = code,
                Message = message,
                Errors = FieldErrorKeys.FromPropertyNames(outcome.Errors)
            });
        }

        /// <summary>
        /// The code and sentence for a setting a bulk type change had to clear.
        /// </summary>
        /// <param name="warning">What was cleared.</param>
        /// <returns>The pair a client receives for the row that carried the setting.</returns>
        public static (string Code, string Message) DescribeWarning(
            VideoStreamBulkWarning warning)
            => warning switch
            {
                VideoStreamBulkWarning.DelayCleared =>
                    (DelayClearedWarningCode,
                        "The delay was cleared because this stream is no longer permanent."),

                VideoStreamBulkWarning.ArchiveCleared =>
                    (ArchiveClearedWarningCode,
                        "The archive length was cleared because this stream is no longer permanent."),

                VideoStreamBulkWarning.RestreamCleared =>
                    (RestreamClearedWarningCode,
                        "The restream settings were cleared because a redirect stream cannot use them."),

                // Unreachable: the enum holds no other member. Thrown rather than answered with
                // a code invented on the spot, because a warning a client has never seen is a
                // silent mismatch somewhere above this line.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(warning),
                    warning,
                    "Unknown video stream bulk warning.")
            };
    }
}
