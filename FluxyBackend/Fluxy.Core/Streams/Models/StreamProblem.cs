namespace Fluxy.Core.Streams.Models
{
    /// <summary>
    /// Immutable business representation of one problem reported by the stream producer: which
    /// configured stream failed and what it said about the failure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="StreamId"/> is a real reference to the configured stream.</b> Unlike the
    /// refusal log, a problem is evidence about one stream of this installation, so the row
    /// points at <c>video_streams</c> and disappears with it. No preview name is stored on the
    /// row: the name is read through the join when a page is built, so the list always shows
    /// the stream's current name rather than a snapshot taken when the problem happened.
    /// </para>
    /// <para>
    /// <b>The row is written by the stream producer.</b> A producer that fails to play a channel
    /// records what it said here; this pass has no HTTP surface for writing, only the storage,
    /// the reader and the whole-table clear.
    /// </para>
    /// </remarks>
    public sealed record StreamProblem
    {
        /// <summary>Identifier of the problem record.</summary>
        public required Guid Id { get; init; }

        /// <summary>Configured stream the producer reported the problem for.</summary>
        public required Guid StreamId { get; init; }

        /// <summary>Reported failure message, at most 1000 characters.</summary>
        public required string ErrorMessage { get; init; }

        /// <summary>Creation timestamp of the row.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>Last modification timestamp of the row.</summary>
        public required DateTimeOffset UpdatedAt { get; init; }
    }
}
