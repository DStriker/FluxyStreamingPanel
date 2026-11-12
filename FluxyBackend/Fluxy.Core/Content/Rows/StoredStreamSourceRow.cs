namespace Fluxy.Core.Content.Rows
{
    /// <summary>
    /// One candidate address of a video stream, shaped for the replacement scan.
    /// </summary>
    /// <remarks>
    /// The stream identifier and its name travel with every source row because the refusal is
    /// judged per stream, not per address: one unusable result leaves the whole failover chain
    /// as it was. A projection only - it never reaches an HTTP response.
    /// </remarks>
    public sealed record StoredStreamSourceRow
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
