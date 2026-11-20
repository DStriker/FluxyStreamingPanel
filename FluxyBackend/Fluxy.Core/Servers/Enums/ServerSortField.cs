namespace Fluxy.Core.Servers.Enums
{
    /// <summary>
    /// Which column of the server table a caller asked to order by.
    /// </summary>
    /// <remarks>
    /// Every member is a column the table draws and the query can honestly sort on. The
    /// identifier is deliberately absent - it is a random uuid for every row but the main one,
    /// and an order on it is an order nothing asked for.
    /// </remarks>
    public enum ServerSortField
    {
        /// <summary>Display name, compared case sensitively by the database collation.</summary>
        Name,

        /// <summary>Whether the server is active or disabled.</summary>
        Status,

        /// <summary>Measured round-trip latency, with an unmeasured server sorted as the zero it stores.</summary>
        Latency,

        /// <summary>Host name, or nothing when the server has none.</summary>
        Hostname,

        /// <summary>The address the server is reached at.</summary>
        Ip,

        /// <summary>The client ceiling, with zero (unlimited) sorting before every real number.</summary>
        ClientCount,

        /// <summary>When the row was written.</summary>
        CreatedAt
    }
}
