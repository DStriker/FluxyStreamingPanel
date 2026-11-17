namespace Fluxy.Core.Transcoding.Enums
{
    /// <summary>
    /// How an admin operation on a transcoding profile ended.
    /// </summary>
    /// <remarks>
    /// The service knows only these words. Which of them is a 201, which a 404 and which a
    /// 409 is the transport's answer, stated beside the response table in the API layer.
    /// </remarks>
    public enum TranscodingProfileAction
    {
        /// <summary>The profile was created.</summary>
        Created,

        /// <summary>The profile's options were written.</summary>
        Updated,

        /// <summary>The profile was deleted.</summary>
        Removed,

        /// <summary>No profile with that identifier exists.</summary>
        NotFound,

        /// <summary>Another profile already holds that name.</summary>
        AlreadyExists,

        /// <summary>
        /// The profile is referenced by a video stream, an audio stream, an on-demand video or
        /// a series, so deleting it would leave that row pointing at nothing.
        /// </summary>
        InUse,

        /// <summary>One of the values did not pass validation; which one is in the errors.</summary>
        Invalid
    }
}
