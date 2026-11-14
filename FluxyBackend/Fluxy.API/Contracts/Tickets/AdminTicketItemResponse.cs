namespace Fluxy.API.Contracts.Tickets
{
    /// <summary>
    /// One ticket as a row of the administrator's list: whose conversation it is, what it is
    /// about, and when it last moved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="Status"/> is viewer-relative.</b> The wire word is derived from the stored
    /// lifecycle and the operator's own unread flag, so the same row can read <c>unread</c> for
    /// one operator and <c>read</c> for another. A closed conversation always reads
    /// <c>closed</c> whatever the flag says.
    /// </para>
    /// <para>
    /// <see cref="Id"/> and <see cref="UserId"/> are text because a JavaScript client parses a
    /// uuid as a number otherwise. They are unrelated: the first is the conversation's own key,
    /// the second the account that opened it.
    /// </para>
    /// </remarks>
    public sealed record AdminTicketItemResponse
    {
        /// <summary>Identifier of the ticket, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Identifier of the account that opened it, as text.</summary>
        public required string UserId { get; init; }

        /// <summary>Current login name of that account, resolved on read.</summary>
        public required string Username { get; init; }

        /// <summary>Subject line the ticket was opened with.</summary>
        public required string Subject { get; init; }

        /// <summary>State of the ticket from this administrator's side.</summary>
        public required string Status { get; init; }

        /// <summary>When the ticket was opened.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// When the conversation last moved: the newest message, or the creation instant when
        /// the thread is empty.
        /// </summary>
        public required DateTimeOffset LastReplyAt { get; init; }
    }
}
