namespace Fluxy.Core.Models.Users
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
    public enum UserGroupSortOrder
    {
        /// <summary>Smallest first: A before Z, oldest before newest.</summary>
        Ascending,

        /// <summary>Largest first: Z before A, newest before oldest.</summary>
        Descending
    }

    /// <summary>
    /// The numbers the admin group list endpoint accepts.
    /// </summary>
    /// <remarks>
    /// The same pair of numbers as <see cref="AdminUserListLimits"/> and for the same
    /// reason: transport and service name one pair of values instead of carrying two copies
    /// that will eventually stop agreeing. Bounds are refused rather than clamped - a caller
    /// who asked for 500 rows deserves to be told it will not get them rather than be handed
    /// 100 alongside a <c>total</c> that does not match the items it was given.
    /// </remarks>
    public static class UserGroupListLimits
    {
        /// <summary>Groups one page holds when the caller names no size.</summary>
        public const int DefaultPageSize = 20;

        /// <summary>Most groups one page may hold.</summary>
        public const int MaxPageSize = 100;

        /// <summary>Most characters one search may carry.</summary>
        public const int MaxSearchLength = 200;
    }
}
