namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// The numbers the admin user list endpoint accepts.
    /// </summary>
    /// <remarks>
    /// Deliberately the same pair of numbers as <c>SessionHistoryLimits</c>, for the same
    /// reason that type exists at all: transport and service name one pair of values instead
    /// of carrying two copies that will eventually stop agreeing.
    ///
    /// The bounds are a rule about a read, not about a secret, and they are refused rather
    /// than clamped. A caller who asked for 500 rows deserves to be told it will not get them
    /// instead of quietly receiving 100 and reporting a <c>total</c> that does not match the
    /// items it was handed.
    /// </remarks>
    public static class AdminUserListLimits
    {
        /// <summary>Accounts one page holds when the caller names no size.</summary>
        public const int DefaultPageSize = 20;

        /// <summary>Most accounts one page may hold.</summary>
        public const int MaxPageSize = 100;

        /// <summary>
        /// Most characters one search may carry.
        /// </summary>
        /// <remarks>
        /// A bound on a read rather than on a secret, and refused rather than truncated: a term
        /// longer than this is a caller with a wrong idea of the endpoint, not somebody looking
        /// for a very long address. It also keeps an oversized value from being copied straight
        /// into a SQL parameter, which is the only reason the number is small rather than large.
        /// </remarks>
        public const int MaxSearchLength = 200;
    }
}
