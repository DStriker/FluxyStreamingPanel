using Fluxy.API.Contracts.Common;
using Fluxy.Core.Content.Enums;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts.Content
{
    /// <summary>
    /// The one place a <see cref="BouquetAction"/> becomes an HTTP status and a code.
    /// </summary>
    /// <remarks>
    /// The service knows nothing about HTTP and this class knows nothing about bouquets beyond
    /// which word maps to which answer, which is the whole reason both exist: a rule can change
    /// without anybody having to decide what status code it should start returning, and a
    /// status can move without touching a rule. The codes follow the ones the categories use
    /// one level deeper, so a client that knows those knows the shape of these.
    /// </remarks>
    public static class BouquetResponses
    {
        /// <summary>Code reported once a bouquet has been created.</summary>
        public const string CreatedCode = "bouquet_created";

        /// <summary>Code reported once a bouquet has been renamed.</summary>
        public const string UpdatedCode = "bouquet_updated";

        /// <summary>Code reported once a bouquet has been deleted.</summary>
        public const string DeletedCode = "bouquet_deleted";

        /// <summary>Code reported once the bouquets have been re-ordered.</summary>
        public const string ReorderedCode = "bouquets_reordered";

        /// <summary>Code reported once one kind of a bouquet's content has been written.</summary>
        public const string ContentSetCode = "bouquet_content_set";

        /// <summary>Code reported when no bouquet answers to that identifier.</summary>
        public const string NotFoundCode = "bouquet_not_found";

        /// <summary>Code reported when another bouquet already holds that name.</summary>
        public const string AlreadyExistsCode = "bouquet_already_exists";

        /// <summary>
        /// Code reported when a bouquet still holds content. The refusal names how many items,
        /// because "why can't I delete it?" and "what do I empty first?" are the same question
        /// asked at two different moments.
        /// </summary>
        public const string NotEmptyCode = "bouquet_not_empty";

        /// <summary>Answer describing <paramref name="outcome"/>.</summary>
        /// <param name="outcome">What the service reported.</param>
        /// <returns>The status, the body, and the rejected fields when the input was the problem.</returns>
        public static (int Status, MessageResponse Body) Describe(BouquetOutcome outcome)
        {
            var (status, code, message) = outcome.Action switch
            {
                BouquetAction.Created =>
                    (StatusCodes.Status201Created, CreatedCode, "The bouquet has been created."),

                BouquetAction.Updated =>
                    (StatusCodes.Status200OK, UpdatedCode, "The bouquet has been renamed."),

                BouquetAction.Removed =>
                    (StatusCodes.Status200OK, DeletedCode, "The bouquet has been deleted."),

                BouquetAction.Reordered =>
                    (StatusCodes.Status200OK, ReorderedCode, "The bouquets have been reordered."),

                BouquetAction.ContentSet =>
                    (StatusCodes.Status200OK, ContentSetCode, "The content of the bouquet has been saved."),

                BouquetAction.NotFound =>
                    (StatusCodes.Status404NotFound,
                        NotFoundCode,
                        "There is no bouquet with that identifier."),

                BouquetAction.AlreadyExists =>
                    (StatusCodes.Status409Conflict,
                        AlreadyExistsCode,
                        "That bouquet name is already taken."),

                // The count is in the message rather than in a field of its own: it is one
                // number in one sentence, and a client that branches on the code does not need
                // it while a person reading the sentence does.
                BouquetAction.NotEmpty =>
                    (StatusCodes.Status409Conflict,
                        NotEmptyCode,
                        outcome.ItemCount == 1
                            ? "1 item is still in this bouquet. Empty it before deleting it."
                            : outcome.ItemCount + " items are still in this bouquet. Empty it before deleting it."),

                BouquetAction.Invalid =>
                    (StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Some of the values you entered are not valid."),

                // Unreachable: the service reports nothing outside this enum. Thrown rather
                // than answered as a 200, because a code a client has never seen with a status
                // it was never told to expect is a silent failure somewhere above this line.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome.Action,
                    "Unknown bouquet outcome.")
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
