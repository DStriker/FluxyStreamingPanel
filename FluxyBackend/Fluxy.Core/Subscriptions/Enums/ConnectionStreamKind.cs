namespace Fluxy.Core.Subscriptions.Enums
{
    /// <summary>
    /// The medium a streaming connection delivered: a live video stream or a live audio stream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A closed set, and deliberately two members.</b> The two media are two resources with two
    /// tables and two routes, so a connection names the one it delivered rather than carrying a
    /// free-form kind that a reader would have to interpret. A third medium would be a third table
    /// and a third route, and it would extend this enum in the same change.
    /// </para>
    /// <para>
    /// Stored as <c>smallint</c> like every other enum in this solution. The numeric values are
    /// schema: <c>VideoStream = 1</c> and <c>AudioStream = 2</c> are already written into rows, so
    /// they must not be renumbered.
    /// </para>
    /// </remarks>
    public enum ConnectionStreamKind
    {
        /// <summary>A live video stream, resolved against the <c>video_streams</c> table.</summary>
        VideoStream = 1,

        /// <summary>A live audio stream, resolved against the <c>audio_streams</c> table.</summary>
        AudioStream = 2
    }
}
