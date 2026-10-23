namespace Fluxy.Core.Streams.Enums
{
    /// <summary>
    /// How importing a playlist ended as a whole.
    /// </summary>
    /// <remarks>
    /// <see cref="Completed"/> covers both a full import and a partial one: the entries that
    /// could not be used are reported in the outcome's problems, so a client learns what
    /// happened to each one without the whole request being called a failure.
    /// </remarks>
    public enum VideoStreamImportStatus
    {
        /// <summary>The readable entries were processed; the counts say how.</summary>
        Completed,

        /// <summary>One of the settings shared by the whole import is invalid.</summary>
        Invalid,

        /// <summary>Another request took a name between the check and the write; nothing landed.</summary>
        Conflict,

        /// <summary>The uploaded file was empty.</summary>
        PlaylistEmpty,

        /// <summary>The uploaded file is an HLS playlist rather than a stream list.</summary>
        PlaylistHls,

        /// <summary>The uploaded file holds more entries than the limit.</summary>
        PlaylistTooManyEntries,

        /// <summary>The uploaded file could not be read as a playlist at all.</summary>
        PlaylistUnreadable
    }
}
