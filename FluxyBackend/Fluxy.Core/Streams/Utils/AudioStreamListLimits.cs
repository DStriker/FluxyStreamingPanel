namespace Fluxy.Core.Streams.Utils
{
    /// <summary>
    /// The bounds of one page of the audio streams table.
    /// </summary>
    /// <remarks>
    /// The ceiling is the same one every paged table uses. It is also what lets the table's
    /// "select everything" checkbox hand one whole page to the bulk endpoint, which accepts no
    /// more identifiers than a page can hold.
    /// </remarks>
    public static class AudioStreamListLimits
    {
        /// <summary>How many streams a page holds when the caller names no size.</summary>
        public const int DefaultPageSize = 20;

        /// <summary>Largest page a caller may ask for.</summary>
        public const int MaxPageSize = 1000;

        /// <summary>Longest search string the table accepts.</summary>
        public const int MaxSearchLength = 200;
    }
}
