using Fluxy.Core.Tickets.Enums;

namespace Fluxy.Core.Tickets.ReadModels
{
    /// <summary>
    /// One message of a support ticket conversation, as the thread screen draws it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="AuthorUsername"/> is resolved on read, never stored.</b> It is the account's
    /// current login name, and it is null when the author has been deleted - the message stays,
    /// because the reference is <c>SET NULL</c> rather than a cascade. The thread still draws the
    /// text and the side, which are the two facts the conversation is made of.
    /// </para>
    /// <para>
    /// <b><see cref="ReadAt"/> is the per-message read mark.</b> Null means the side that did not
    /// write it has not read it yet; the value is set by the service when that side reads the
    /// thread.
    /// </para>
    /// </remarks>
    public sealed record TicketReplyItem
    {
        /// <summary>Identifier of the message.</summary>
        public required Guid Id { get; init; }

        /// <summary>Account that wrote it, or null once that account has been deleted.</summary>
        public Guid? AuthorId { get; init; }

        /// <summary>Current login name of that account, or null when it no longer exists.</summary>
        public string? AuthorUsername { get; init; }

        /// <summary>Message body, at most 4000 characters.</summary>
        public required string Message { get; init; }

        /// <summary>Whether the operator wrote it rather than the account.</summary>
        public required bool IsAdminReply { get; init; }

        /// <summary>Moment the other side read it, or null while it is still unread.</summary>
        public DateTimeOffset? ReadAt { get; init; }

        /// <summary>When the message was written.</summary>
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
