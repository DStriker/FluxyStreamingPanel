namespace Fluxy.Core.Users.Utils
{
    /// <summary>
    /// The numbers the admin group list endpoint accepts.
    /// </summary>
    /// <remarks>
    /// The same pair of numbers as <see cref="AdminUserListLimits"/> and for the same
    /// reason: transport and service name one pair of values instead of carrying two copies
    /// that will eventually stop agreeing. Bounds are refused rather than clamped - a caller
    /// who asked for 5000 rows deserves to be told it will not get them rather than be handed
    /// 1000 alongside a <c>total</c> that does not match the items it was given.
    /// </remarks>
    public static class UserGroupListLimits
    {
        /// <summary>Groups one page holds when the caller names no size.</summary>
        public const int DefaultPageSize = 20;

        /// <summary>
        /// Most groups one page may hold.
        /// </summary>
        /// <remarks>
        /// A thousand for the reason <c>AdminUserListLimits.MaxPageSize</c> gives: the table's
        /// "select everything" checkbox hands its selection to the bulk endpoint, and the two
        /// ceilings must be the same one.
        /// </remarks>
        public const int MaxPageSize = 1000;

        /// <summary>Most characters one search may carry.</summary>
        public const int MaxSearchLength = 200;
    }
}
