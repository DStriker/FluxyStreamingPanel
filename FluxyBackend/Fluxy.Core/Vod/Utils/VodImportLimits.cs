namespace Fluxy.Core.Vod.Utils
{
    /// <summary>
    /// The bounds of one on-demand playlist import.
    /// </summary>
    /// <remarks>
    /// The file bound is a request bound as much as a business one: ten megabytes is far more
    /// than a text list of five thousand addresses needs, and refusing it before parsing keeps
    /// a mistaken upload from becoming a long request. The entry bound is the point at which
    /// one import stops being one operator decision and becomes a batch worth splitting.
    /// </remarks>
    public static class VodImportLimits
    {
        /// <summary>Largest playlist file accepted, in bytes.</summary>
        public const int MaxFileBytes = 10 * 1024 * 1024;

        /// <summary>Most entries one import accepts.</summary>
        public const int MaxEntries = 5000;

        /// <summary>Prefix of the name an entry without a title or <c>tvg-name</c> gets.</summary>
        public const string FallbackNamePrefix = "Video ";
    }
}
