using Fluxy.Core.Tickets.Enums;

namespace Fluxy.Core.Tickets.ReadModels
{
    /// <summary>
    /// One support ticket as a row of the account's own list: the subject it was opened with, its
    /// lifecycle, whether it holds something new for that account, and when the conversation last
    /// moved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="Status"/> is the stored lifecycle, not the word a reader sees.</b> The account
    /// reading this row is one of the conversation's two sides, so the wire word is derived from
    /// the lifecycle and <see cref="UserUnread"/> by <c>TicketStatuses</c>, where a closed ticket
    /// always beats the flag.
    /// </para>
    /// <para>
    /// <b><see cref="LastReplyAt"/> falls back to the ticket's own creation instant.</b> A ticket
    /// with no message yet is placed by when it arrived rather than left at the bottom of the list
    /// with no time at all, and the same value is what the default order reads.
    /// </para>
    /// </remarks>
    public sealed record TicketListItem
    {
        /// <summary>Identifier of the ticket.</summary>
        public required Guid Id { get; init; }

        /// <summary>Subject line the ticket was opened with.</summary>
        public required string Subject { get; init; }

        /// <summary>Lifecycle of the conversation.</summary>
        public required TicketStatus Status { get; init; }

        /// <summary>Whether the conversation holds a message this account has not read.</summary>
        public required bool UserUnread { get; init; }

        /// <summary>When the ticket was opened.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// When the conversation last moved: the newest message, or <see cref="CreatedAt"/> when
        /// the conversation holds none.
        /// </summary>
        public required DateTimeOffset LastReplyAt { get; init; }
    }
}
