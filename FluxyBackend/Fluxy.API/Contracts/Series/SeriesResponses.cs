using Fluxy.API.Contracts.Common;
using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Outcomes;
using Fluxy.Core.Vod.Enums;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts.Series
{
    /// <summary>
    /// The one place a series outcome becomes an HTTP status and a code.
    /// </summary>
    /// <remarks>
    /// The service knows nothing about HTTP and this class knows nothing about series beyond
    /// which word maps to which answer. The codes follow the ones the streams and the on-demand
    /// catalogue already use, one level deeper.
    /// </remarks>
    public static class SeriesResponses
    {
        /// <summary>Code reported once a series has been created.</summary>
        public const string CreatedCode = "series_created";

        /// <summary>Code reported once a series has been changed.</summary>
        public const string UpdatedCode = "series_updated";

        /// <summary>Code reported once a series has been deleted.</summary>
        public const string DeletedCode = "series_deleted";

        /// <summary>Code reported when no series answers to that identifier.</summary>
        public const string NotFoundCode = "series_not_found";

        /// <summary>Code reported when another series of the same category holds that name.</summary>
        public const string AlreadyExistsCode = "series_already_exists";

        /// <summary>Code reported once an episode has been created.</summary>
        public const string EpisodeCreatedCode = "series_episode_created";

        /// <summary>Code reported once an episode has been changed.</summary>
        public const string EpisodeUpdatedCode = "series_episode_updated";

        /// <summary>Code reported once an episode has been deleted.</summary>
        public const string EpisodeDeletedCode = "series_episode_deleted";

        /// <summary>Code reported when no episode answers to that identifier.</summary>
        public const string EpisodeNotFoundCode = "series_episode_not_found";

        /// <summary>Code reported when the season-and-number pair is already taken.</summary>
        public const string EpisodeAlreadyExistsCode = "series_episode_already_exists";

        /// <summary>Code reported once a whole season has been created.</summary>
        public const string SeasonCreatedCode = "series_season_created";

        /// <summary>Code reported when the season already has episodes.</summary>
        public const string SeasonAlreadyExistsCode = "series_season_already_exists";

        /// <summary>Code reported when this installation has no transcoder.</summary>
        public const string TranscodeUnavailableCode = "transcode_not_configured";

        /// <summary>Answer describing a series operation.</summary>
        public static (int Status, MessageResponse Body) Describe(SeriesOutcome outcome)
        {
            var (status, code, message) = outcome.Action switch
            {
                SeriesAction.Created =>
                    (StatusCodes.Status201Created, CreatedCode, "The series has been created."),

                SeriesAction.Updated =>
                    (StatusCodes.Status200OK, UpdatedCode, "The series has been updated."),

                SeriesAction.Removed =>
                    (StatusCodes.Status200OK, DeletedCode, "The series has been deleted."),

                SeriesAction.NotFound =>
                    (StatusCodes.Status404NotFound,
                        NotFoundCode,
                        "There is no series with that identifier."),

                SeriesAction.AlreadyExists =>
                    (StatusCodes.Status409Conflict,
                        AlreadyExistsCode,
                        "That series name is already taken within this category."),

                SeriesAction.Invalid =>
                    (StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Some of the values you entered are not valid."),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome.Action,
                    "Unknown series outcome.")
            };

            return (status, new MessageResponse
            {
                Code = code,
                Message = message,
                Errors = FieldErrorKeys.FromPropertyNames(outcome.Errors)
            });
        }

        /// <summary>Answer describing an episode operation.</summary>
        public static (int Status, MessageResponse Body) Describe(EpisodeOutcome outcome)
        {
            var (status, code, message) = outcome.Action switch
            {
                EpisodeAction.Created =>
                    (StatusCodes.Status201Created,
                        EpisodeCreatedCode,
                        "The episode has been created."),

                EpisodeAction.Updated =>
                    (StatusCodes.Status200OK,
                        EpisodeUpdatedCode,
                        "The episode has been updated."),

                EpisodeAction.Removed =>
                    (StatusCodes.Status200OK,
                        EpisodeDeletedCode,
                        "The episode has been deleted."),

                EpisodeAction.NotFound =>
                    (StatusCodes.Status404NotFound,
                        EpisodeNotFoundCode,
                        "There is no episode with that identifier in this series."),

                EpisodeAction.AlreadyExists =>
                    (StatusCodes.Status409Conflict,
                        EpisodeAlreadyExistsCode,
                        "Another episode of this series already has that season and number."),

                EpisodeAction.Invalid =>
                    (StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Some of the values you entered are not valid."),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome.Action,
                    "Unknown episode outcome.")
            };

            return (status, new MessageResponse
            {
                Code = code,
                Message = message,
                Errors = FieldErrorKeys.FromPropertyNames(outcome.Errors)
            });
        }

        /// <summary>Answer describing a season creation.</summary>
        public static (int Status, MessageResponse Body) Describe(SeasonOutcome outcome)
        {
            var (status, code, message) = outcome.Action switch
            {
                SeasonAction.Created =>
                    (StatusCodes.Status201Created,
                        SeasonCreatedCode,
                        "The season has been created."),

                SeasonAction.NotFound =>
                    (StatusCodes.Status404NotFound,
                        NotFoundCode,
                        "There is no series with that identifier."),

                SeasonAction.AlreadyExists =>
                    (StatusCodes.Status409Conflict,
                        SeasonAlreadyExistsCode,
                        "This season already has episodes."),

                SeasonAction.Invalid =>
                    (StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Some of the values you entered are not valid."),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome.Action,
                    "Unknown season outcome.")
            };

            return (status, new MessageResponse
            {
                Code = code,
                Message = message,
                Errors = FieldErrorKeys.FromPropertyNames(outcome.Errors)
            });
        }

        /// <summary>Builds the 201 body that carries the count of created episodes.</summary>
        public static SeasonCreatedResponse CreatedSeason(SeasonOutcome outcome) => new()
        {
            Code = SeasonCreatedCode,
            Message = "The season has been created.",
            SeriesId = outcome.SeriesId.ToString(),
            SeasonNumber = outcome.SeasonNumber,
            AddedCount = outcome.AddedCount,
            FirstEpisodeNumber = outcome.AddedCount > 0 ? 1 : 0,
            LastEpisodeNumber = outcome.AddedCount
        };

        /// <summary>Answer describing a request to transcode one episode.</summary>
        public static (int Status, MessageResponse Body) Describe(EpisodeTranscodeOutcome outcome)
        {
            var (status, code, message) = outcome.Action switch
            {
                VodTranscodeAction.Unavailable =>
                    (StatusCodes.Status503ServiceUnavailable,
                        TranscodeUnavailableCode,
                        "This server has no transcoder configured, so the episode cannot be " +
                        "converted yet."),

                VodTranscodeAction.Invalid =>
                    (StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Some of the values you entered are not valid."),

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
    }
}
