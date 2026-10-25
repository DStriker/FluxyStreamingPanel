using Fluxy.Core.Vod.Enums;

namespace Fluxy.Core.Series.Outcomes
{
    /// <summary>
    /// How a request to transcode one episode ended.
    /// </summary>
    /// <remarks>
    /// The action and the format are the shared on-demand enums: the two resources offer the
    /// same transcode surface, and a second pair with the same members would be two places to
    /// keep in step. No job identifier exists in this pass because nothing is queued.
    /// </remarks>
    public sealed record EpisodeTranscodeOutcome
    {
        /// <summary>How the request ended.</summary>
        public required VodTranscodeAction Action { get; init; }

        /// <summary>The format the request named.</summary>
        public required VodTranscodeFormat Format { get; init; }

        /// <summary>Rejected fields, when the request named a format that does not exist.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
