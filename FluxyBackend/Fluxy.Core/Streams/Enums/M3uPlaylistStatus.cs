namespace Fluxy.Core.Streams.Enums
{
    /// <summary>
    /// How reading an uploaded playlist ended before anything was written.
    /// </summary>
    /// <remarks>
    /// The four refusals are separate because they ask the operator to do four different
    /// things: choose a file with content, choose a stream playlist rather than an HLS one,
    /// make the file smaller, or split it into several imports. A single "invalid playlist"
    /// would leave them guessing which.
    /// </remarks>
    public enum M3uPlaylistStatus
    {
        /// <summary>The text was read, and its entries and problems are the answer.</summary>
        Parsed,

        /// <summary>The file held nothing but whitespace.</summary>
        Empty,

        /// <summary>The file is an HLS playlist, not a list of streams.</summary>
        Hls,

        /// <summary>The file holds more entries than one import accepts.</summary>
        TooManyEntries,

        /// <summary>The file could not be read as a playlist at all.</summary>
        Unreadable
    }
}
