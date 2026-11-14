namespace Fluxy.API.Contracts.Tickets
{
    /// <summary>
    /// One ticket in full as the administrator reads it: the account that opened it, its
    /// lifecycle, and the whole conversation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="Status"/> is the word for the operator's side.</b> It derives from the
    /// stored lifecycle and the operator's own unread flag, so the same conversation can read
    /// differently on the account's screen - which is what "viewer relative" means.
    /// </para>
    /// <para>
    /// <see cref="UpdatedAt"/> is the row's last write of any kind, while
    /// <see cref="LastReplyAt"/> is the newest message. Both travel because the thread prints
    /// one and the list prints the other, and the fallback of the second to
    /// <see cref="CreatedAt"/> keeps a ticket nobody answered from having no instant at all.
    /// </para>
    /// </remarks>
    public sealed record AdminTicketDetailResponse
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
