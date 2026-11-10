namespace Fluxy.Core.Streams.ReadModels
{
    /// <summary>
    /// One stream problem as a row of the admin table: which stream, what the producer reported,
    /// and when.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="StreamName"/> is resolved on read, not stored.</b> The row points at a real
    /// stream, so the name is read through the join while the page is built and always shows the
    /// stream's current name. It is null only when a read path did not resolve it - the full list
    /// endpoint always does - and never a stored snapshot, which the schema deliberately does not
    /// carry.
    /// </para>
    /// <para>
    /// <see cref="ErrorMessage"/> is the stored message, trimmed and cut to its column at the write
    /// boundary, so the row stays legible on its own.
    /// </para>
    /// </remarks>
    public sealed record StreamProblemListItem
    {
        /// <summary>Identifier of the problem record.</summary>
        public required Guid Id { get; init; }

        /// <summary>Configured stream the problem is about.</summary>
        public required Guid StreamId { get; init; }

        /// <summary>
        /// Display name of that stream, resolved on read, or null when the read path did not
        /// resolve it.
        /// </summary>
        public string? StreamName { get; init; }

        /// <summary>Reported failure message, at most 1000 characters.</summary>
        public required string ErrorMessage { get; init; }

        /// <summary>When the problem was recorded.</summary>
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
