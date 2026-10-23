namespace Fluxy.Core.Streams.Utils
{
    /// <summary>
    /// The bounds of one M3U import of audio streams.
    /// </summary>
    /// <remarks>
    /// A playlist of audio entries is a few kilobytes per thousand lines, so the file limit is
    /// generous rather than tight; the entry limit exists so one upload cannot occupy the
    /// process for an unbounded time. The parser is shared with the video import, and the entry
    /// limit is passed to it explicitly rather than relying on its default.
    /// </remarks>
    public static class AudioStreamImportLimits
    {
        /// <summary>Largest playlist one import accepts, in bytes.</summary>
        public const int MaxFileBytes = 10 * 1024 * 1024;

        /// <summary>Most entries one import accepts.</summary>
        public const int MaxEntries = 5000;
    }
}
