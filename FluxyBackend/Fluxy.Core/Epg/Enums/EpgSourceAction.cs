namespace Fluxy.Core.Epg.Enums
{
    /// <summary>
    /// How an admin operation on a programme guide source ended.
    /// </summary>
    /// <remarks>
    /// The service knows only these words. Which of them is a 201, which a 404 and which a
    /// 409 is the transport's answer, stated beside the other response tables in the API
    /// layer.
    /// </remarks>
    public enum EpgSourceAction
    {
        /// <summary>The source was created.</summary>
        Created,

        /// <summary>The change was written to the row.</summary>
        Updated,

        /// <summary>The source was deleted.</summary>
        Removed,

        /// <summary>No source with that identifier.</summary>
        NotFound,

        /// <summary>Another source already holds that name.</summary>
        AlreadyExists,

        /// <summary>A configured stream still reads its guide from this source.</summary>
        InUse,

        /// <summary>One of the values did not pass validation; which one is in the errors.</summary>
        Invalid
    }
}
