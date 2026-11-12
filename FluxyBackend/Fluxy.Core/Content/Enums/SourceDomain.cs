namespace Fluxy.Core.Content.Enums
{
    /// <summary>
    /// The four kinds of playback address a source replacement reaches.
    /// </summary>
    /// <remarks>
    /// The members are the stable order the answer reports its counts in, whatever the
    /// request found. Ordering them here rather than at the transport is what keeps a client
    /// from rendering the four domains in a different order on every run.
    /// </remarks>
    public enum SourceDomain
    {
        /// <summary>The addresses a video stream relays from.</summary>
        VideoStreams = 0,

        /// <summary>The addresses an audio stream relays from.</summary>
        AudioStreams = 1,

        /// <summary>The source of an on-demand video.</summary>
        VideoOnDemand = 2,

        /// <summary>The source of a series episode.</summary>
        SeriesEpisodes = 3
    }
}
