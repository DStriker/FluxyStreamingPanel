namespace Fluxy.Core.Tickets.Models
{
    /// <summary>
    /// Immutable business representation of one message in a support ticket conversation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A message has one of two sides.</b> <see cref="IsAdminReply"/> says whether the operator
    /// wrote it or the account did; that is the fact the ticket's two unread flags are derived
    /// from besides the row itself. The side is stored, not inferred from the author's current
    /// role: an account that was an operator when it answered stays on the operator side of the
    /// conversation even if its role changes later.
    /// </para>
    /// <para>
    /// <b><see cref="AuthorId"/> may be null.</b> The reference is <c>SET NULL</c> when an account
    /// is deleted, because the message is part of the conversation and may not disappear with the
    /// account that wrote it; the side and the text remain legible without the author.
    /// </para>
    /// <para>
    /// <b><see cref="ReadAt"/> is the per-message read mark.</b> It is set by the service when the
    /// message's side reads the thread, and null until then. The row stores the fact; a page reads
    /// it to draw which messages are new.
    /// </para>
    /// </remarks>
    public sealed record TicketReply
    {
        /// <summary>Identifier of the message.</summary>
        public required Guid Id { get; init; }

        /// <summary>Ticket the message belongs to.</summary>
        public required Guid TicketId { get; init; }

        /// <summary>Account that wrote it, or null once that account has been deleted.</summary>
        public Guid? AuthorId { get; init; }

        /// <summary>Message body, at most 4000 characters.</summary>
        public required string Message { get; init; }

        /// <summary>Whether the operator wrote it rather than the account.</summary>
        public required bool IsAdminReply { get; init; }

        /// <summary>Moment the message's side read it, or null while it is still unread.</summary>
        public DateTimeOffset? ReadAt { get; init; }

        /// <summary>Creation timestamp of the row.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>Last modification timestamp of the row.</summary>
        public required DateTimeOffset UpdatedAt { get; init; }
    }
}
