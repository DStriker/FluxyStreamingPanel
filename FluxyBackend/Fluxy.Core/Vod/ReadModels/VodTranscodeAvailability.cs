using Fluxy.Core.Vod.Enums;

namespace Fluxy.Core.Vod.ReadModels
{
    /// <summary>
    /// Whether this installation can transcode, and which formats it offers.
    /// </summary>
    /// <remarks>
    /// The answer is a fact about the server rather than about one item, so the client asks it
    /// once and can draw the format menu disabled with a reason instead of offering buttons
    /// whose every press would be refused. <see cref="Formats"/> is stated even when
    /// <see cref="Available"/> is false: the menu still has five names to show, and a client
    /// should not hardcode them.
    /// </remarks>
    public sealed record VodTranscodeAvailability
    {
        /// <summary>Whether a transcoder is configured.</summary>
        public required bool Available { get; init; }

        /// <summary>The formats the request may name, in the order the menu draws them.</summary>
        public required IReadOnlyList<VodTranscodeFormat> Formats { get; init; }
    }
}
