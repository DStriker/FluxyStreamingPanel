namespace Fluxy.Core.Blacklists.Enums
{
    /// <summary>
    /// How an admin operation on a blacklist entry ended.
    /// </summary>
    /// <remarks>
    /// The service knows only these words. Which of them is a 201, which a 404 and which a
    /// 409 is the transport's answer, stated beside the other response tables in the API
    /// layer.
    /// </remarks>
    public enum BlacklistEntryAction
    {
        /// <summary>The entry was created.</summary>
        Created,

        /// <summary>The value and the note were written to the row.</summary>
        Updated,

        /// <summary>The entry was deleted.</summary>
        Removed,

        /// <summary>Every entry of one list was deleted at once.</summary>
        Cleared,

        /// <summary>No entry with that identifier exists in that list.</summary>
        NotFound,

        /// <summary>The same list already holds that value.</summary>
        AlreadyExists,

        /// <summary>One of the values did not pass validation; which one is in the errors.</summary>
        Invalid
    }
}
