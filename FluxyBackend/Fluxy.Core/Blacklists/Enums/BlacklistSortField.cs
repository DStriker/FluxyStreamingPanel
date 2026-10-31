namespace Fluxy.Core.Blacklists.Enums
{
    /// <summary>
    /// Which column of a blacklist table a caller asked to order by.
    /// </summary>
    /// <remarks>
    /// Every member is a column the table draws and the query can honestly sort on. The
    /// identifier and the note are deliberately absent: the identifier is a random uuid and an
    /// order on it is an order nothing asked for, while the note is free text whose order
    /// carries no meaning. The counter and the added date are both offered, and the added date
    /// is the default.
    /// </remarks>
    public enum BlacklistSortField
    {
        /// <summary>The value the list matches on, compared as text.</summary>
        Value,

        /// <summary>How many attempts the entry has blocked, largest or smallest first.</summary>
        BlockedAttempts,

        /// <summary>When the entry was added.</summary>
        CreatedAt
    }
}
