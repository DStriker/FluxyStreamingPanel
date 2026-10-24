namespace Fluxy.Core.Vod.Enums
{
    /// <summary>
    /// How a request to transcode one item ended.
    /// </summary>
    /// <remarks>
    /// The encoding itself is not part of this pass. What exists is the surface an operator
    /// reaches - the five formats and the request - and the honest answer that this
    /// installation has no transcoder configured. The member is named for the reason rather
    /// than for a job that would never run.
    /// </remarks>
    public enum VodTranscodeAction
    {
        /// <summary>No transcoder is configured, so nothing was queued.</summary>
        Unavailable,

        /// <summary>The requested format names no member of <see cref="VodTranscodeFormat"/>.</summary>
        Invalid
    }
}
