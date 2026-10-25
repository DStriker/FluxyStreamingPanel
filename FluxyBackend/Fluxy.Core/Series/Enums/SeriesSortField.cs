namespace Fluxy.Core.Series.Enums
{
    /// <summary>
    /// Which column of the series table a caller asked to order by.
    /// </summary>
    /// <remarks>
    /// <see cref="Seasons"/> and <see cref="Episodes"/> order by aggregates over the episode
    /// rows, because neither number is stored on the series row. The identifier is deliberately
    /// absent: it is a random uuid, and an order on it is an order nothing asked for.
    /// </remarks>
    public enum SeriesSortField
    {
        /// <summary>Display name, compared case sensitively by the database collation.</summary>
        Name,

        /// <summary>The category's display name, which is what the column draws.</summary>
        Category,

        /// <summary>State of the series: Inactive or Active.</summary>
        Status,

        /// <summary>Release date, with series that name none grouped last in both directions.</summary>
        ReleaseDate,

        /// <summary>Rating, with series that name none grouped last in both directions.</summary>
        Rating,

        /// <summary>The largest season number the series holds, or zero.</summary>
        Seasons,

        /// <summary>How many episode rows the series holds.</summary>
        Episodes
    }
}
