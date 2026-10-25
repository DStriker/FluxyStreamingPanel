namespace Fluxy.Core.Series.Enums
{
    /// <summary>
    /// How an admin operation on an episode ended.
    /// </summary>
    public enum EpisodeAction
    {
        /// <summary>The episode was created.</summary>
        Created,

        /// <summary>The change was written to the row.</summary>
        Updated,

        /// <summary>The episode was deleted.</summary>
        Removed,

        /// <summary>No episode with that identifier.</summary>
        NotFound,

        /// <summary>Another episode of the same series already holds that season and number.</summary>
        AlreadyExists,

        /// <summary>One of the values did not pass validation; which one is in the errors.</summary>
        Invalid
    }
}
