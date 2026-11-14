namespace Fluxy.API.Contracts.Tickets
{
    /// <summary>
    /// One ticket and its whole conversation as its owner reads it.
    /// </summary>
    /// <remarks>
    /// The shape is the administrator's detail minus the two facts that make a row somebody
    /// else's - the owner's identifier and name - because on this side the owner is the reader.
    /// <see cref="LastReplyAt"/> still falls back to <see cref="CreatedAt"/> while the thread is
    /// empty, so the page and the list agree about where a fresh ticket belongs.
    /// </remarks>
    public sealed record TicketDetailResponse
    {
        /// <summary>Identifier of the ticket, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Subject line the ticket was opened with.</summary>
        public required string Subject { get; init; }

        /// <summary>State of the ticket from this viewer's side.</summary>
        public required string Status { get; init; }

        /// <summary>When the ticket was opened.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>When the ticket row itself was last changed.</summary>
        public required DateTimeOffset UpdatedAt { get; init; }

        /// <summary>
        /// When the conversation last moved: the newest message, or <see cref="CreatedAt"/> when
        /// the thread holds none.
        /// </summary>
        public required DateTimeOffset LastReplyAt { get; init; }

        /// <summary>The conversation, oldest message first.</summary>
        public required IReadOnlyList<TicketReplyResponse> Replies { get; init; }
    }
}
