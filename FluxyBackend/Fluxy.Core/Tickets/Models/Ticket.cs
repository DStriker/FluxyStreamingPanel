using Fluxy.Core.Tickets.Enums;

namespace Fluxy.Core.Tickets.Models
{
    /// <summary>
    /// Immutable business representation of one support ticket: a conversation an account opened
    /// with the operator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="Status"/> is the lifecycle, not the word a reader sees.</b> A ticket is either
    /// open or closed; whether the conversation reads as unread or read is a fact about one of its
    /// two sides, not about the ticket. The wire word is derived per viewer by
    /// <c>TicketStatuses</c>, where a closed ticket always reads as closed whatever the flags say.
    /// </para>
    /// <para>
    /// <b>The two unread flags belong to the two sides of the conversation.</b>
    /// <see cref="AdminUnread"/> is raised by a message the account wrote and cleared when the
    /// operator reads the thread; <see cref="UserUnread"/> is raised by a reply from the operator
    /// and cleared when the account reads it. Keeping both on the ticket row means one page of a
    /// list can answer "is this unread for me" without touching the message table.
    /// </para>
    /// </remarks>
    public sealed record Ticket
    {
        /// <summary>Identifier of the ticket.</summary>
        public required Guid Id { get; init; }

        /// <summary>Account that opened the ticket and owns the conversation.</summary>
        public required Guid UserId { get; init; }

        /// <summary>Subject line the ticket was opened with, at most 200 characters.</summary>
        public required string Subject { get; init; }

        /// <summary>Lifecycle of the conversation.</summary>
        public required TicketStatus Status { get; init; }

        /// <summary>Whether the conversation holds something the operator has not read yet.</summary>
        public required bool AdminUnread { get; init; }

        /// <summary>Whether the conversation holds something the account has not read yet.</summary>
        public required bool UserUnread { get; init; }

        /// <summary>Creation timestamp of the row.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>Last modification timestamp of the row.</summary>
        public required DateTimeOffset UpdatedAt { get; init; }
    }
}
