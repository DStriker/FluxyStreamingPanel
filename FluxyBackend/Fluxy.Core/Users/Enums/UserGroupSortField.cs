namespace Fluxy.Core.Users.Enums
{
    /// <summary>
    /// Which column of the admin group list a caller asked to order by.
    /// </summary>
    /// <remarks>
    /// The list is ordered by the server rather than by the browser, because the browser only
    /// ever holds one page: an order applied to the rows already on screen would leave rows
    /// on the next page in whatever order the query happened to return them.
    ///
    /// The identifier is deliberately absent for the same reason it is absent from the users
    /// list - a random uuid is an order nothing asked for. <see cref="PermissionsCount"/> is
    /// a count over the child table rather than a column, which is why it is listed here
    /// explicitly: it is a fact the server knows, so the server orders by it.
    /// </remarks>
    public enum UserGroupSortField
    {
        /// <summary>Display name, compared case sensitively by the database collation.</summary>
        Name,

        /// <summary>Access level: Client &lt; Reseller &lt; Admin.</summary>
        Role,

        /// <summary>
        /// State of the group: Unregistered, Registered or Blocked. The members are not
        /// ordered, but the column is - an order on an enum stored as smallint is an order on
        /// its numbers.
        /// </summary>
        Status,

        /// <summary>How many permissions the group grants.</summary>
        PermissionsCount,

        /// <summary>When the group was created, oldest first by default.</summary>
        CreatedAt
    }

    /// <summary>Which way the admin group list runs.</summary>
}
