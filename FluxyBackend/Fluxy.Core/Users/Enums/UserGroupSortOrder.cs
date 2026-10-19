using Fluxy.Core.Users.Utils;

namespace Fluxy.Core.Users.Enums
{
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
}
