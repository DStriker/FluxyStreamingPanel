namespace Fluxy.Core.Blacklists.Enums
{
    /// <summary>
    /// Which of the three access-control lists an entry belongs to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The numeric values are fixed and are what the <c>type</c> column stores, so a value
    /// already in the database is a fact about the past as much as about the code. Unlike the
    /// other enums of this project the first member is <c>0</c> rather than <c>1</c>: an entry
    /// whose type was never written is an <c>ip</c> entry, which is the list an operator
    /// reaches for first, and the three members are a partition of one table rather than a
    /// scale.
    /// </para>
    /// <para>
    /// The members are not ordered - one is not "more" than another - so nothing anywhere may
    /// compare two of them.
    /// </para>
    /// </remarks>
    public enum BlacklistType
    {
        /// <summary>A single address, IPv4 or IPv6, that is refused.</summary>
        Ip = 0,

        /// <summary>An internet service provider whose traffic is refused.</summary>
        Isp = 1,

        /// <summary>A user agent string that is refused.</summary>
        UserAgent = 2
    }
}
