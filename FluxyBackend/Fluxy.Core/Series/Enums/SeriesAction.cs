namespace Fluxy.Core.Series.Enums
{
    /// <summary>
    /// How an admin operation on a series ended.
    /// </summary>
    /// <remarks>
    /// The service knows only these words. Which of them is a 201, which is a 409 and which
    /// code a client branches on is the transport's answer.
    /// </remarks>
    public enum SeriesAction
    {
        /// <summary>The series was created.</summary>
        Created,

        /// <summary>The change was written to the row.</summary>
        Updated,

        /// <summary>The series was deleted together with its episodes.</summary>
        Removed,

        /// <summary>No series with that identifier.</summary>
        NotFound,

        /// <summary>Another series of the same category already holds that name.</summary>
        AlreadyExists,

        /// <summary>One of the values did not pass validation; which one is in the errors.</summary>
        Invalid
    }
}
