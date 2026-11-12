namespace Fluxy.Core.Content.Rows
{
    /// <summary>
    /// One candidate address of an audio stream, shaped for the replacement scan.
    /// </summary>
    /// <remarks>
    /// The audio twin of <see cref="StoredStreamSourceRow"/>: the two lists are separate tables
    /// with separate screens, and the scan names each one rather than folding them together.
    /// </remarks>
    public sealed record StoredAudioSourceRow
    {
        /// <summary>Identifier of the stream the address belongs to.</summary>
        public required Guid StreamId { get; init; }

        /// <summary>Display name of that stream, read for the refusal line.</summary>
        public required string StreamName { get; init; }

        /// <summary>Position in the failover chain, zero based.</summary>
        public required int Position { get; init; }

        /// <summary>The address itself, as stored.</summary>
        public required string Url { get; init; }
    }
}
