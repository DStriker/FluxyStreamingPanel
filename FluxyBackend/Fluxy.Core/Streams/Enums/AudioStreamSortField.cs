namespace Fluxy.Core.Streams.Enums
{
    /// <summary>Column the audio streams table is ordered by.</summary>
    /// <remarks>
    /// The member names are the words the API accepts, parsed from plain text so the
    /// conventional <c>sortBy=category</c> works without spelling an enum member in full.
    /// </remarks>
    public enum AudioStreamSortField
    {
        /// <summary>Display name.</summary>
        Name,

        /// <summary>Display name of the category.</summary>
        Category,

        /// <summary>Whether the stream is active.</summary>
        Status,

        /// <summary>The kind of stream.</summary>
        Type
    }
}
