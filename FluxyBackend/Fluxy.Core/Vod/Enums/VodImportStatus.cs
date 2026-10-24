namespace Fluxy.Core.Vod.Enums
{
    /// <summary>
    /// How reading an uploaded playlist ended before anything was written.
    /// </summary>
    /// <remarks>
    /// The refusals are separate because they ask the operator to do different things: choose a
    /// file with content, choose a stream list rather than an HLS one, make the file smaller, or
    /// split it. A single "invalid playlist" would leave them guessing which.
    /// </remarks>
    public enum VodImportStatus
    {
        /// <summary>The text was read, and its entries and problems are the answer.</summary>
        Completed,

        /// <summary>One of the settings shared by the batch was refused.</summary>
        Invalid,

        /// <summary>Another request took a name between the check and the write.</summary>
        Conflict,

        /// <summary>The file held nothing but whitespace.</summary>
        PlaylistEmpty,

        /// <summary>The file is an HLS playlist, not a list of videos.</summary>
        PlaylistHls,

        /// <summary>The file holds more entries than one import accepts.</summary>
        PlaylistTooManyEntries,

        /// <summary>The file could not be read as a playlist at all.</summary>
        PlaylistUnreadable
    }
}
