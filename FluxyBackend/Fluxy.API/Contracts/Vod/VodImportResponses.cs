using Fluxy.API.Contracts.Common;
using Fluxy.Core.Vod.Enums;
using Fluxy.Core.Vod.Outcomes;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts.Vod
{
    /// <summary>
    /// The one place a playlist import outcome becomes an HTTP status and a code.
    /// </summary>
    /// <remarks>
    /// The service counts entries and names lines; this class decides that a readable file is
    /// a <c>200</c> and a file that is not a video list is a <c>400</c> with a code of its own.
    /// </remarks>
    public static class VodImportResponses
    {
        /// <summary>Code reported when the readable entries of a playlist have been processed.</summary>
        public const string CompletedCode = "video_on_demand_import_completed";

        /// <summary>Code reported when the uploaded file is empty.</summary>
        public const string EmptyCode = "video_on_demand_import_empty";

        /// <summary>Code reported when the uploaded file is an HLS playlist.</summary>
        public const string HlsCode = "video_on_demand_import_hls";

        /// <summary>Code reported when the uploaded file is larger than the limit.</summary>
        public const string TooLargeCode = "video_on_demand_import_too_large";

        /// <summary>Code reported when the playlist holds more entries than one import accepts.</summary>
        public const string TooManyEntriesCode = "video_on_demand_import_too_many_entries";

        /// <summary>Code reported when the file could not be read as a playlist.</summary>
        public const string UnreadableCode = "video_on_demand_import_unreadable";

        /// <summary>Code reported when another request took a name before the batch was written.</summary>
        public const string ConflictCode = "video_on_demand_import_conflict";

        /// <summary>Answer describing <paramref name="outcome"/>.</summary>
        /// <param name="outcome">What the service reported.</param>
        /// <returns>
        /// The status and the body. A completed import carries the counts; every other answer
        /// is the protocol's usual <see cref="MessageResponse"/>.
        /// </returns>
        public static (int Status, object Body) Describe(VodImportOutcome outcome)
        {
            if (outcome.Status is VodImportStatus.Completed)
            {
                return (StatusCodes.Status200OK, new VodImportResponse
                {
                    Code = CompletedCode,
                    Message = "The playlist has been imported.",
                    Total = outcome.Total,
                    Imported = outcome.Imported,
                    Skipped = outcome.Skipped,
                    Failed = outcome.Failed,
                    Problems = outcome.Problems.Select(Describe).ToList()
                });
            }

            var (status, code, message) = outcome.Status switch
            {
                VodImportStatus.Invalid =>
                    (StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Some of the values you entered are not valid."),

                VodImportStatus.Conflict =>
                    (StatusCodes.Status409Conflict,
                        ConflictCode,
                        "A video name was taken while the playlist was being imported. " +
                        "Nothing was imported; try again."),

                VodImportStatus.PlaylistEmpty =>
                    (StatusCodes.Status400BadRequest,
                        EmptyCode,
                        "The playlist file is empty."),

                VodImportStatus.PlaylistHls =>
                    (StatusCodes.Status400BadRequest,
                        HlsCode,
                        "HLS playlists cannot be imported. Upload a list of videos instead."),

                VodImportStatus.PlaylistTooManyEntries =>
                    (StatusCodes.Status400BadRequest,
                        TooManyEntriesCode,
                        "The playlist holds more entries than one import accepts. " +
                        "Split it into smaller files."),

                VodImportStatus.PlaylistUnreadable =>
                    (StatusCodes.Status400BadRequest,
                        UnreadableCode,
                        "The playlist could not be read. Upload an M3U file with at least " +
                        "one video entry."),

                // Unreachable: the service reports nothing outside this enum.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome.Status,
                    "Unknown on-demand import outcome.")
            };

            return (status, new MessageResponse
            {
                Code = code,
                Message = message,
                Errors = FieldErrorKeys.FromPropertyNames(outcome.Errors)
            });
        }

        /// <summary>The body of a file that was refused before the service saw it.</summary>
        /// <param name="code">One of the codes of this class.</param>
        /// <param name="message">English sentence naming what the operator should change.</param>
        public static MessageResponse Failure(string code, string message) => new()
        {
            Code = code,
            Message = message
        };

        /// <summary>Describes one entry that did not land.</summary>
        private static VodImportProblemResponse Describe(VodImportProblem problem)
        {
            var skipped = problem.Action is VodAction.AlreadyExists;

            var (code, message) = problem.Action switch
            {
                VodAction.AlreadyExists =>
                    (VodResponses.AlreadyExistsCode,
                        "That video name is already taken within this category."),

                VodAction.Invalid =>
                    ("validation_failed",
                        problem.Message ?? "Some of the values of this playlist entry are not valid."),

                // Unreachable: a problem is either a skipped name or a refused entry.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(problem),
                    problem.Action,
                    "Unknown on-demand import problem.")
            };

            return new VodImportProblemResponse
            {
                Line = problem.LineNumber,
                Name = problem.Name,
                Skipped = skipped,
                Code = code,
                Message = message,
                Errors = FieldErrorKeys.FromPropertyNames(problem.Errors)
            };
        }
    }
}
