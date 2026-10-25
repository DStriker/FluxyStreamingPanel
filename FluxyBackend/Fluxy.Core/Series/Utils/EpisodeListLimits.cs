namespace Fluxy.Core.Series.Utils
{
    /// <summary>
    /// The numbers the episode table endpoint accepts.
    /// </summary>
    public static class EpisodeListLimits
    {
        /// <summary>Episodes one page holds when the caller names no size.</summary>
        public const int DefaultPageSize = 20;

        /// <summary>
        /// Most episodes one page may hold, following the same ceiling as every other table
        /// so a bulk selection can cover the whole page.
        /// </summary>
        public const int MaxPageSize = 1000;

        /// <summary>Most characters one search may carry.</summary>
        public const int MaxSearchLength = 200;
    }
}
