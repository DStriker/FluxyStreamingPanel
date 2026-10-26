namespace Fluxy.Core.Epg.Enums
{
    /// <summary>
    /// Which column of the guide source table a caller asked to order by.
    /// </summary>
    /// <remarks>
    /// Every member is a column the table draws and the query can honestly sort on. The
    /// identifier is deliberately absent - it is a random uuid, and an order on it is an order
    /// nothing asked for.
    /// </remarks>
    public enum EpgSourceSortField
    {
        /// <summary>Display name, compared case sensitively by the database collation.</summary>
        Name,

        /// <summary>The address the guide is fetched from.</summary>
        Url,

        /// <summary>How many days of programmes are kept.</summary>
        DaysKeep,

        /// <summary>Moment of the last refresh, with sources never refreshed grouped last.</summary>
        LastEpgUpdatedAt
    }
}
