namespace Fluxy.Core.Series.Utils
{
    /// <summary>
    /// The bounds of one bulk operation over episodes.
    /// </summary>
    public static class EpisodeBulkLimits
    {
        /// <summary>Most episodes one bulk request may name.</summary>
        public const int MaxItems = EpisodeListLimits.MaxPageSize;
    }
}
