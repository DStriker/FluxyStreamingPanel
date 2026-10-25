namespace Fluxy.Core.Series.Enums
{
    /// <summary>
    /// How a request to create a whole season from a folder ended.
    /// </summary>
    /// <remarks>
    /// A season is not a stored row: it is the set of episodes carrying one season number.
    /// The action therefore describes a batch of episode rows rather than a season entity.
    /// </remarks>
    public enum SeasonAction
    {
        /// <summary>The episodes of the season were created.</summary>
        Created,

        /// <summary>No series with that identifier.</summary>
        NotFound,

        /// <summary>The season already has episodes; it cannot be created twice.</summary>
        AlreadyExists,

        /// <summary>The folder or another value did not pass validation.</summary>
        Invalid
    }
}
