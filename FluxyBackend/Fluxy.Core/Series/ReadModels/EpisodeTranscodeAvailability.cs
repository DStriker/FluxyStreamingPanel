using Fluxy.Core.Vod.Enums;

namespace Fluxy.Core.Series.ReadModels
{
    /// <summary>
    /// Whether this installation can transcode episodes, and which formats it offers.
    /// </summary>
    /// <remarks>
    /// The format enum is shared with the on-demand catalogue rather than duplicated: the two
    /// resources offer the same five containers, and a second enum with the same members would
    /// be two places to add a sixth. The answer is a fact about the server, so the episode
    /// table asks it once and draws the action disabled with the reason.
    /// </remarks>
    public sealed record EpisodeTranscodeAvailability
    {
        /// <summary>Whether a transcoder is configured.</summary>
        public required bool Available { get; init; }

        /// <summary>The formats the request may name, in the order the menu draws them.</summary>
        public required IReadOnlyList<VodTranscodeFormat> Formats { get; init; }
    }
}
