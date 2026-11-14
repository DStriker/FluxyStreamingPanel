using Fluxy.Core.Tickets.Enums;

namespace Fluxy.Core.Tickets.ReadModels
{
    /// <summary>
    /// One support ticket as a row of the operator's list: which account opened it, under which
    /// subject, and whether it holds something the operator has not read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="Username"/> is resolved on read, never stored.</b> The ticket points at a real
    /// account, so the name is joined while the page is built and always shows the account's
    /// current login name. The listing is scoped to the roles the operator may read before the
    /// count is taken, so a ticket of an account outside that scope is not on the list and not in
    /// <see cref="AdminTicketPage.Total"/> either.
    /// </para>
    /// <para>
    /// <b><see cref="Status"/> is the stored lifecycle.</b> The word the operator sees is derived
    /// from it and <see cref="AdminUnread"/> by <c>TicketStatuses</c>; <c>closed</c> beats the flag,
    /// and the account's own flag never decides what the operator's row reads.
    /// </para>
    /// </remarks>
    public sealed record AdminTicketListItem
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

        /// <summary>When the ticket was opened.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// When the conversation last moved: the newest message, or <see cref="CreatedAt"/> when
        /// the conversation holds none.
        /// </summary>
        public required DateTimeOffset LastReplyAt { get; init; }
    }
}
