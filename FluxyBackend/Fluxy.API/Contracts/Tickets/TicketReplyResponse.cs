namespace Fluxy.API.Contracts.Tickets
{
    /// <summary>
    /// One message of a ticket's conversation as the thread screen draws it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="IsAdminReply"/> decides which side of the chat the bubble sits on</b>, and
    /// it is a signed fact of the message rather than the author's current role - an operator
    /// account that later changes role does not move its old messages to the other side.
    /// </para>
    /// <para>
    /// <b><see cref="ReadAt"/> is the receipt of this message, not of the thread.</b> Null means
    /// the other side has not read it yet. <see cref="AuthorId"/> and <see cref="AuthorName"/>
    /// are null for a message whose author has been deleted: the conversation keeps what was
    /// said, and the row says the author is no longer there.
    /// </para>
    /// </remarks>
    public sealed record TicketReplyResponse
    {
        /// <summary>Identifier of the message, as text.</summary>
        public required string Id { get; init; }

        /// <summary>The text the author wrote.</summary>
        public required string Message { get; init; }

        /// <summary>Whether an administrator wrote this message. Fixed for the row.</summary>
        public required bool IsAdminReply { get; init; }

        /// <summary>Identifier of the author, as text, or null when the account no longer exists.</summary>
        public string? AuthorId { get; init; }

        /// <summary>Current login name of the author, or null when the account no longer exists.</summary>
        public string? AuthorName { get; init; }

        /// <summary>When the other side read this message, or null while it is unread.</summary>
        public DateTimeOffset? ReadAt { get; init; }

        /// <summary>When the message was written.</summary>
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
