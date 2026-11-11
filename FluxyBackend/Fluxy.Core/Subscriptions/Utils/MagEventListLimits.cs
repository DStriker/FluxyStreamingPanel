namespace Fluxy.Core.Subscriptions.Utils
{
    /// <summary>
    /// Column width of a MAG event row and the bounds of the endpoints that read and write it.
    /// </summary>
    /// <remarks>
    /// The limits live in this layer rather than beside the table configuration so the transport
    /// and the send rule can judge a value without referencing the data access assembly, and the
    /// configuration uses the same constants, so a column width and the rule that refuses an
    /// oversized value cannot drift apart.
    /// </remarks>
    public static class MagEventListLimits
    {
        /// <summary>Longest message a row may store.</summary>
        public const int MessageMaxLength = 1000;

        /// <summary>Items one page holds when the caller names no size.</summary>
        public const int DefaultPageSize = 20;

        /// <summary>Most items one page may hold.</summary>
        public const int MaxPageSize = 1000;

        /// <summary>Most characters one search may carry.</summary>
        public const int MaxSearchLength = 200;
    }
}
