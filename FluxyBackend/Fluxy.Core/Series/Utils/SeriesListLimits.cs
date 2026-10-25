namespace Fluxy.Core.Series.Utils
{
    /// <summary>
    /// The numbers the series table endpoint accepts.
    /// </summary>
    public static class SeriesListLimits
    {
        /// <summary>Series one page holds when the caller names no size.</summary>
        public const int DefaultPageSize = 20;

        /// <summary>
        /// Most series one page may hold. A thousand for the reason every other table gives:
        /// the table's "select everything" checkbox hands its selection to the bulk endpoint,
        /// and <see cref="SeriesBulkLimits.MaxItems"/> follows this number.
        /// </summary>
        public const int MaxPageSize = 1000;

        /// <summary>Most characters one search may carry.</summary>
        public const int MaxSearchLength = 200;
    }
}
