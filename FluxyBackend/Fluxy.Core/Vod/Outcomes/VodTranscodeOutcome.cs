using Fluxy.Core.Vod.Enums;

namespace Fluxy.Core.Vod.Outcomes
{
    /// <summary>
    /// How a request to transcode one item ended.
    /// </summary>
    /// <remarks>
    /// No job identifier yet: this pass has no transcoder, so a request is either refused
    /// because the installation cannot transcode or because the format names nothing. The
    /// record is the seam the real implementation will fill in.
    /// </remarks>
    public sealed record VodTranscodeOutcome
    {
        /// <summary>How the request ended.</summary>
        public required VodTranscodeAction Action { get; init; }

        /// <summary>The format the request named.</summary>
        public required VodTranscodeFormat Format { get; init; }

        /// <summary>Rejected fields, when the request named a format that does not exist.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
