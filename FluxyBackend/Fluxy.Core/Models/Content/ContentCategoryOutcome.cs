namespace Fluxy.Core.Models.Content
{
    /// <summary>
    /// How an admin operation on a content category ended.
    /// </summary>
    /// <remarks>
    /// The service knows only these words. Which of them is a 201, which is a 409 and which
    /// code a client branches on is the transport's answer, stated beside the other response
    /// tables in the API layer - so a status can change without touching a rule, and a rule
    /// can change without anybody having to decide what status code it should start
    /// returning.
    /// </remarks>
    public enum ContentCategoryAction
    {
        /// <summary>The category was created.</summary>
        Created,

        /// <summary>The change was written to the row.</summary>
        Updated,

        /// <summary>The category was deleted.</summary>
        Removed,

        /// <summary>The order of the type's categories was rewritten.</summary>
        Reordered,

        /// <summary>No category with that identifier.</summary>
        NotFound,

        /// <summary>Another category of the same type already holds that name.</summary>
        AlreadyExists,

        /// <summary>One of the values did not pass validation; which one is in the errors.</summary>
        Invalid,
    }

    /// <summary>
    /// The outcome of one admin operation on a content category: which
    /// <see cref="ContentCategoryAction"/> ended it, which category it was about, and which
    /// fields were refused when the input was the problem.
    /// </summary>
    /// <remarks>
    /// <see cref="Errors"/> is keyed by the C# name of the property the service rejected, the
    /// convention every other service here follows - the transport turns those into the
    /// camelCase names a form reads, in one place.
    /// </remarks>
    public sealed record ContentCategoryOutcome
    {
        /// <summary>How the operation ended.</summary>
        public required ContentCategoryAction Action { get; init; }

        /// <summary>
        /// Category the operation was about. <see cref="Guid.Empty"/> for an outcome that
        /// never reached a row, such as a refusal made from the body alone.
        /// </summary>
        public Guid CategoryId { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
