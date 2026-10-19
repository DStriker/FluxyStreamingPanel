namespace Fluxy.Core.Content.Enums
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

        /// <summary>
        /// The category is filed under by at least one content row, so deleting it would
        /// strand them. The database would refuse it anyway - the key is
        /// <c>ON DELETE RESTRICT</c> - but the service checks first so that the answer is a
        /// code and a count rather than a provider's exception.
        /// </summary>
        InUse,
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
}
