namespace Fluxy.Core.Users.Utils
{
    /// <summary>
    /// Bounds of the endpoint that reads the credit log.
    /// </summary>
    /// <remarks>
    /// They live in this layer rather than beside the table configuration so the transport can
    /// judge a request without referencing the data access assembly. The page bounds are the ones
    /// every list of this API shares; the ceiling is deliberately 1000, refused rather than
    /// clamped.
    /// </remarks>
    public static class CreditLogListLimits
    {
        /// <summary>Items one page holds when the caller names no size.</summary>
        public const int DefaultPageSize = 20;

        /// <summary>Most items one page may hold.</summary>
        public const int MaxPageSize = 1000;

        /// <summary>Most characters one search may carry.</summary>
        public const int MaxSearchLength = 200;
    }
}
