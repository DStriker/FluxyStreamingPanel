using Fluxy.Core.Content.Enums;
using Fluxy.API.Contracts.Common;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts.Content
{
    /// <summary>
    /// The one place a <see cref="ContentCategoryAction"/> becomes an HTTP status and a code.
    /// </summary>
    /// <remarks>
    /// The service knows nothing about HTTP and this class knows nothing about categories
    /// beyond which word maps to which answer, which is the whole reason both exist: a rule
    /// can change without anybody having to decide what status code it should start
    /// returning, and a status can move without touching a rule.
    ///
    /// The codes follow the ones the accounts and the groups already use -
    /// <c>user_already_exists</c>, <c>user_group_not_found</c> - one level deeper, so a
    /// client that knows those two sets knows the shape of this one without reading a third
    /// table of exceptions.
    /// </remarks>
    public static class ContentCategoryResponses
    {
        /// <summary>Code reported once a category has been created.</summary>
        public const string CreatedCode = "content_category_created";

        /// <summary>Code reported once a category has been changed.</summary>
        public const string UpdatedCode = "content_category_updated";

        /// <summary>Code reported once a category has been deleted.</summary>
        public const string DeletedCode = "content_category_deleted";

        /// <summary>Code reported once a type's order has been rewritten.</summary>
        public const string ReorderedCode = "content_categories_reordered";

        /// <summary>Code reported when no category answers to that identifier.</summary>
        public const string NotFoundCode = "content_category_not_found";

        /// <summary>Code reported when another category of the same type already holds that name.</summary>
        public const string AlreadyExistsCode = "content_category_already_exists";

        /// <summary>
        /// Code reported when a category still has streams filed under it. The refusal names
        /// how many, because "why can't I delete it?" and "what do I move first?" are the same
        /// question asked at two different moments.
        /// </summary>
        public const string InUseCode = "content_category_in_use";

        /// <summary>Answer describing <paramref name="outcome"/>.</summary>
        /// <param name="outcome">What the service reported.</param>
        /// <returns>The status, the body, and the rejected fields when the input was the problem.</returns>
        public static (int Status, MessageResponse Body) Describe(ContentCategoryOutcome outcome)
        {
            var (status, code, message) = outcome.Action switch
            {
                ContentCategoryAction.Created =>
                    (StatusCodes.Status201Created, CreatedCode, "The category has been created."),

                ContentCategoryAction.Updated =>
                    (StatusCodes.Status200OK, UpdatedCode, "The category has been updated."),

                ContentCategoryAction.Removed =>
                    (StatusCodes.Status200OK, DeletedCode, "The category has been deleted."),

                ContentCategoryAction.Reordered =>
                    (StatusCodes.Status200OK, ReorderedCode, "The categories have been reordered."),

                ContentCategoryAction.NotFound =>
                    (StatusCodes.Status404NotFound,
                        NotFoundCode,
                        "There is no category with that identifier."),

                ContentCategoryAction.AlreadyExists =>
                    (StatusCodes.Status409Conflict,
                        AlreadyExistsCode,
                        "That category name is already taken within this content type."),

                // The count is in the message rather than in a field of its own: it is one
                // number in one sentence, and a client that branches on the code does not
                // need it while a person reading the sentence does.
                ContentCategoryAction.InUse =>
                    (StatusCodes.Status409Conflict,
                        InUseCode,
                        outcome.UsedCount == 1
                            ? "1 stream is still filed under this category."
                            : outcome.UsedCount + " streams are still filed under this category."),

                ContentCategoryAction.Invalid =>
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
                    "Unknown content category outcome.")
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
