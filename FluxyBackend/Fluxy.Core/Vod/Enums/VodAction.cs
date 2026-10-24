namespace Fluxy.Core.Vod.Enums
{
    /// <summary>
    /// How an admin operation on an on-demand item ended.
    /// </summary>
    /// <remarks>
    /// The service knows only these words. Which of them is a 201, which is a 409 and which
    /// code a client branches on is the transport's answer, stated beside the other response
    /// tables in the API layer.
    /// </remarks>
    public enum VodAction
    {
        /// <summary>The item was created.</summary>
        Created,

        /// <summary>The change was written to the row.</summary>
        Updated,

        /// <summary>The item was deleted.</summary>
        Removed,

        /// <summary>No item with that identifier.</summary>
        NotFound,

        /// <summary>Another item of the same category already holds that name.</summary>
        AlreadyExists,

        /// <summary>One of the values did not pass validation; which one is in the errors.</summary>
        Invalid
    }
}
