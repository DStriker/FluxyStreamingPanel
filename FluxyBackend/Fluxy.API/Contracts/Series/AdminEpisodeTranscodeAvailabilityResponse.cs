namespace Fluxy.API.Contracts.Series
{
    /// <summary>
    /// Whether this installation can transcode episodes, and the formats the menu offers.
    /// </summary>
    public sealed record AdminEpisodeTranscodeAvailabilityResponse
    {
        /// <summary>Whether a transcoder is configured.</summary>
        public required bool Available { get; init; }

        /// <summary>Format names, in the order the menu draws them.</summary>
        public required IReadOnlyList<string> Formats { get; init; }
    }
}
