namespace Fluxy.Core.Vod.Enums
{
    /// <summary>
    /// Which column of the on-demand table a caller asked to order by.
    /// </summary>
    /// <remarks>
    /// Every member is a value the query can honestly sort on. The identifier is deliberately
    /// absent - it is a random uuid, and an order on it is an order nothing asked for.
    /// </remarks>
    public enum VodSortField
    {
        /// <summary>Display name, compared case sensitively by the database collation.</summary>
        Name,

        /// <summary>The category's display name, which is what the column draws.</summary>
        Category,

        /// <summary>State of the item: Inactive or Active.</summary>
        Status,

        /// <summary>How the item is delivered: Relay or Redirect.</summary>
        Type,

        /// <summary>Release date, with items that name none grouped last in both directions.</summary>
        ReleaseDate,

        /// <summary>Rating, with items that name none grouped last in both directions.</summary>
        Rating
    }
}
