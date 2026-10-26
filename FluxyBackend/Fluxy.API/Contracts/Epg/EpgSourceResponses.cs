using Fluxy.API.Contracts.Common;
using Fluxy.Core.Epg.Enums;
using Fluxy.Core.Epg.Outcomes;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts.Epg
{
    /// <summary>
    /// The one place an <see cref="EpgSourceAction"/> becomes an HTTP status and a code.
    /// </summary>
    /// <remarks>
    /// The service knows nothing about HTTP and this class knows nothing about guides beyond
    /// which word maps to which answer. The codes follow the ones the streams, the series and
    /// the on-demand catalogue already use, one level deeper, so a client that knows those
    /// sets knows the shape of this one.
    /// </remarks>
    public static class EpgSourceResponses
    {
        /// <summary>Code reported once a source has been created.</summary>
        public const string CreatedCode = "epg_source_created";

        /// <summary>Code reported once a source has been changed.</summary>
        public const string UpdatedCode = "epg_source_updated";

        /// <summary>Code reported once a source has been deleted.</summary>
        public const string DeletedCode = "epg_source_deleted";

        /// <summary>Code reported when no source answers to that identifier.</summary>
        public const string NotFoundCode = "epg_source_not_found";

        /// <summary>Code reported when another source already holds that name.</summary>
        public const string AlreadyExistsCode = "epg_source_already_exists";

        /// <summary>Code reported when a stream still reads its guide from the source.</summary>
        public const string InUseCode = "epg_source_in_use";

        /// <summary>Answer describing <paramref name="outcome"/>.</summary>
        /// <param name="outcome">What the service reported.</param>
        /// <returns>The status, the body, and the rejected fields when the input was the problem.</returns>
        public static (int Status, MessageResponse Body) Describe(EpgSourceOutcome outcome)
        {
            var (status, code, message) = outcome.Action switch
            {
                EpgSourceAction.Created =>
                    (StatusCodes.Status201Created, CreatedCode, "The source has been created."),

                EpgSourceAction.Updated =>
                    (StatusCodes.Status200OK, UpdatedCode, "The source has been updated."),

                EpgSourceAction.Removed =>
                    (StatusCodes.Status200OK, DeletedCode, "The source has been deleted."),

                EpgSourceAction.NotFound =>
                    (StatusCodes.Status404NotFound,
                        NotFoundCode,
                        "There is no programme guide source with that identifier."),

                EpgSourceAction.AlreadyExists =>
                    (StatusCodes.Status409Conflict,
                        AlreadyExistsCode,
                        "That source name is already taken."),

                EpgSourceAction.InUse =>
                    (StatusCodes.Status409Conflict,
                        InUseCode,
                        "The source is still used by " + outcome.StreamsUsing +
                            " stream(s). Clear the programme guide on those streams first."),

                EpgSourceAction.Invalid =>
                    (StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Some of the values you entered are not valid."),

                // Unreachable: the service reports nothing outside this enum. Thrown rather
                // than answered as a 200, because a code a client has never seen with a status
                // it was never told to expect is a silent failure somewhere above this line.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome.Action,
                    "Unknown programme guide outcome.")
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
