namespace Fluxy.Core.Content.Enums
{
    /// <summary>
    /// How an admin operation on a bouquet ended.
    /// </summary>
    /// <remarks>
    /// The service knows only these words. Which of them is a 201, which is a 409 and which code
    /// a client branches on is the transport's answer - the same split
    /// <see cref="ContentCategoryAction"/> describes.
    /// </remarks>
    public enum BouquetAction
    {
        /// <summary>The bouquet was created.</summary>
        Created,

        /// <summary>The change was written to the row.</summary>
        Updated,

        /// <summary>The bouquet was deleted.</summary>
        Removed,

        /// <summary>The order of the bouquets was rewritten.</summary>
        Reordered,

        /// <summary>The content of one kind and its order were written.</summary>
        ContentSet,

        /// <summary>No bouquet with that identifier.</summary>
        NotFound,

        /// <summary>Another bouquet already holds that name, compared without regard to case.</summary>
        AlreadyExists,

        /// <summary>One of the values did not pass validation; which one is in the errors.</summary>
        Invalid,

        /// <summary>
        /// The bouquet still holds content, so deleting it would strand the membership rows.
        /// The database would refuse it anyway - the key is <c>ON DELETE RESTRICT</c> - but the
        /// service checks first so that the answer is a code and a count rather than a
        /// provider's exception.
        /// </summary>
        NotEmpty,
    }
}
