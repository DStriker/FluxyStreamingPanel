namespace Fluxy.Core.Series.Enums
{
    /// <summary>
    /// Which column of the episode table a caller asked to order by.
    /// </summary>
    /// <remarks>
    /// The table is always scoped to one series. <see cref="Season"/> and <see cref="Episode"/>
    /// are the natural order; the remaining members order by the metadata a row carries.
    /// </remarks>
    public enum EpisodeSortField
    {
        /// <summary>Season number, with the episode number as the tie-break.</summary>
        Season,

        /// <summary>Episode number, with the season number as the tie-break.</summary>
        Episode,

        /// <summary>Display name, with the season and episode numbers as tie-breaks.</summary>
        Name,

        /// <summary>State of the episode: Inactive or Active.</summary>
        Status,

        /// <summary>How the episode is delivered: Relay or Redirect.</summary>
        Type,

        /// <summary>Release date, with episodes that name none grouped last in both directions.</summary>
        ReleaseDate,

        /// <summary>Rating, with episodes that name none grouped last in both directions.</summary>
        Rating
    }
}
