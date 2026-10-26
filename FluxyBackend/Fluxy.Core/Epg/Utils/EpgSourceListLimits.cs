namespace Fluxy.Core.Epg.Utils
{
    /// <summary>
    /// The numbers the guide source table endpoint accepts.
    /// </summary>
    /// <remarks>
    /// Deliberately the same pair of numbers as every other paged table here. The bounds are a
    /// rule about a read, and they are refused rather than clamped: a caller who asked for 5000
    /// rows deserves to be told it will not get them instead of quietly receiving 1000 and
    /// reporting a total that does not match the items it was handed.
    /// </remarks>
    public static class EpgSourceListLimits
    {
        /// <summary>Items one page holds when the caller names no size.</summary>
        public const int DefaultPageSize = 20;

        /// <summary>Most items one page may hold.</summary>
        public const int MaxPageSize = 1000;

        /// <summary>Most characters one search may carry.</summary>
        public const int MaxSearchLength = 200;
    }
}
