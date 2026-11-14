using Fluxy.Core.Tickets.Enums;

namespace Fluxy.Core.Tickets.ReadModels
{
    /// <summary>
    /// One support ticket with its whole conversation, oldest message first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The ticket's two unread flags are both carried here.</b> The thread screen reads one
    /// conversation, and which word it draws for the status depends on which side is looking: the
    /// account's screen reads <see cref="UserUnread"/>, the operator's reads
    /// <see cref="AdminUnread"/>, and <c>TicketStatuses</c> turns the pair and the lifecycle into
    /// the wire word.
    /// </para>
    /// <para>
    /// <b><see cref="Username"/> is the account that opened the ticket.</b> It is resolved on read,
    /// like every other name in this domain; the operator's screen draws it as the thread's subject
    /// line, and the account's own screen does not need it but receives it anyway because one shape
    /// serves both readings.
    /// </para>
    /// </remarks>
    public sealed record TicketDetail
    {
        /// <summary>Identifier of the ticket.</summary>
        public required Guid Id { get; init; }

        /// <summary>Account that opened the ticket.</summary>
        public required Guid UserId { get; init; }

        /// <summary>Current login name of that account, resolved on read.</summary>
        public required string Username { get; init; }

        /// <summary>Subject line the ticket was opened with.</summary>
        public required string Subject { get; init; }

        /// <summary>Lifecycle of the conversation.</summary>
        public required TicketStatus Status { get; init; }

        /// <summary>Whether the conversation holds a message the operator has not read.</summary>
        public required bool AdminUnread { get; init; }

        /// <summary>Whether the conversation holds a message the account has not read.</summary>
        public required bool UserUnread { get; init; }

        /// <summary>When the ticket was opened.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>When the ticket row was last modified.</summary>
        public required DateTimeOffset UpdatedAt { get; init; }

        /// <summary>The conversation, oldest message first.</summary>
        public required IReadOnlyList<TicketReplyItem> Replies { get; init; }
    }
}
