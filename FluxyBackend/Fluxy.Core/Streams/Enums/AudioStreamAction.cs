namespace Fluxy.Core.Streams.Enums
{
    /// <summary>
    /// How one write to an audio stream ended.
    /// </summary>
    /// <remarks>
    /// The service answers with this rather than an HTTP status, because the rules that decide
    /// an outcome and the mapping from an outcome to a status belong to different layers. The
    /// transport turns each member into a code and a status in one place.
    /// </remarks>
    public enum AudioStreamAction
    {
        /// <summary>The stream was created.</summary>
        Created,

        /// <summary>The stream was changed.</summary>
        Updated,

        /// <summary>The stream was deleted.</summary>
        Removed,

        /// <summary>The stream was started.</summary>
        Started,

        /// <summary>The stream was stopped.</summary>
        Stopped,

        /// <summary>The stream was restarted.</summary>
        Restarted,

        /// <summary>No stream carries the identifier the request named.</summary>
        NotFound,

        /// <summary>Another stream of the same category already carries the name.</summary>
        AlreadyExists,

        /// <summary>One or more fields of the request were refused.</summary>
        Invalid
    }
}
