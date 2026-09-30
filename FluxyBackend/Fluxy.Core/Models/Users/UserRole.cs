namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// Access level of a user. The three levels are ordered: every next one includes the
    /// rights of the previous one.
    /// </summary>
    /// <remarks>
    /// The numeric values are meaningful and encode that hierarchy, so a permission check is
    /// <c>user.Role &gt;= requiredRole</c>. Do not renumber the members - it would change what
    /// existing rows grant.
    /// </remarks>
    public enum UserRole
    {
        /// <summary>End customer of the service. The level every new account starts at.</summary>
        Client = 1,

        /// <summary>Partner who buys and resells the service. Everything a client may do, and more.</summary>
        Reseller = 2,

        /// <summary>Operator of the service. Everything a reseller may do, and more.</summary>
        Admin = 3
    }
}