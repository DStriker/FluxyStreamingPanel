namespace Fluxy.Core.Streams.Enums
{
    /// <summary>How a playlist import as a whole ended.</summary>
    /// <remarks>
    /// <see cref="Completed"/> means the file was read; it says nothing about how many entries
    /// became streams. A readable file whose every name was taken is still completed, because
    /// the request was carried out and the answer is that there was nothing to add.
    /// </remarks>
    public enum AudioStreamImportStatus
    {
        /// <summary>The file was read and its entries were processed.</summary>
        Completed,

        /// <summary>One or more settings shared by the batch were refused.</summary>
        Invalid,

        /// <summary>A name was taken between the check and the write; nothing was stored.</summary>
        Conflict,

        /// <summary>The file held no entries.</summary>
        PlaylistEmpty,

        /// <summary>The file was an HLS playlist rather than a list of streams.</summary>
        PlaylistHls,

        /// <summary>The file held more entries than one import accepts.</summary>
        PlaylistTooManyEntries,

        /// <summary>The file could not be read as a playlist.</summary>
        PlaylistUnreadable
    }
}
