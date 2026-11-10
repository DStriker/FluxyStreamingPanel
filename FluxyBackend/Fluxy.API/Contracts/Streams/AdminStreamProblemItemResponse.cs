namespace Fluxy.API.Contracts.Streams
{
    /// <summary>
    /// One recorded stream problem as a row of the admin table: which stream, what the producer
    /// reported, and when.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The stream's name is resolved on read, never stored.</b> The row carries a real foreign
    /// key to <c>video_streams</c>, so <see cref="StreamName"/> is the stream's current name, read
    /// through the join while the page is built. It is non-null under that design: a problem
    /// without a stream is a row the foreign key does not allow, and deleting the stream deletes
    /// the journal that names it.
    /// </para>
    /// <para>
    /// <see cref="Id"/> and <see cref="StreamId"/> are text because a JavaScript client parses a
    /// uuid as a number otherwise. They are unrelated: the first is this record's own key, the
    /// second the stream the problem is about.
    /// </para>
    /// </remarks>
    public sealed record AdminStreamProblemItemResponse
    {
        /// <summary>Identifier of the problem record, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Identifier of the stream the problem is about, as text.</summary>
        public required string StreamId { get; init; }

        /// <summary>Display name of that stream, resolved through the join on read.</summary>
        public required string StreamName { get; init; }

        /// <summary>Failure message as it was stored, trimmed and cut to its column.</summary>
        public required string ErrorMessage { get; init; }

        /// <summary>When the problem was recorded.</summary>
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
