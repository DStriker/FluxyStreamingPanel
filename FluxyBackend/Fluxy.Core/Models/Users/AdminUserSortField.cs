namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// Which column of the admin user list a caller asked to order by.
    /// </summary>
    /// <remarks>
    /// The list is ordered by the server rather than by the browser, because the browser only
    /// ever holds one page: an order applied to the rows already on screen would leave rows on
    /// the next page in whatever order the query happened to return them, so two pages could
    /// both contain a user or both miss one.
    ///
    /// Every member is a value the query can honestly sort on. The identifier is deliberately
    /// absent - it is a random uuid, and an order on it is an order nothing asked for; a person
    /// who wants a stable row order wants <see cref="Username"/>.
    ///
    /// <see cref="LastSeenAt"/> is the one column that is not stored on the row itself: it is
    /// the creation time of the freshest refresh token of the account, read as a correlated
    /// subquery. That is why it sorts and filters with everything else - it is a fact the
    /// server knows, so the server orders by it - but it is also the reason a page of this
    /// list costs one extra scalar subquery per row.
    /// </remarks>
    public enum AdminUserSortField
    {
        /// <summary>Login name, compared case sensitively by the database collation.</summary>
        Username,

        /// <summary>Email address, stored already normalized.</summary>
        Email,

        /// <summary>Access level: Client &lt; Reseller &lt; Admin.</summary>
        Role,

        /// <summary>
        /// State of the account: Unregistered, Registered or Blocked. The members are not
        /// ordered, but the column is - an order on an enum stored as smallint is an order on
        /// its numbers, which puts Blocked last whatever anybody thinks of the states.
        /// </summary>
        Status,

        /// <summary>When the account last signed in or refreshed, newest by default.</summary>
        LastSeenAt,

        /// <summary>Address of the network that visit came from, as text.</summary>
        Ip
    }
}
