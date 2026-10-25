namespace Fluxy.Core.Series.Utils
{
    /// <summary>
    /// The bounds of one bulk operation over series.
    /// </summary>
    public static class SeriesBulkLimits
    {
        /// <summary>Most series one bulk request may name.</summary>
        public const int MaxItems = SeriesListLimits.MaxPageSize;
    }
}
