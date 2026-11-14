namespace Fluxy.API.Contracts.Tickets
{
    /// <summary>
    /// One ticket as a row of the account's own list: the subject it was opened with, its state
    /// from this viewer's side, and when the conversation last moved.
    /// </summary>
    /// <remarks>
    /// The list carries no author, because every row belongs to the account reading it.
    /// <see cref="LastReplyAt"/> falls back to <see cref="CreatedAt"/> while the thread is empty,
    /// so a freshly opened ticket is placed by when it arrived rather than at the bottom of the
    /// list with no instant at all.
    /// </remarks>
    public sealed record TicketItemResponse
    {
        /// <summary>Identifier of the ticket, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Subject line the ticket was opened with.</summary>
        public required string Subject { get; init; }

        /// <summary>State of the ticket from this viewer's side.</summary>
        public required string Status { get; init; }

        /// <summary>When the ticket was opened.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// When the conversation last moved: the newest message, or <see cref="CreatedAt"/> when
        /// the thread holds none.
        /// </summary>
        public required DateTimeOffset LastReplyAt { get; init; }
    }
}
