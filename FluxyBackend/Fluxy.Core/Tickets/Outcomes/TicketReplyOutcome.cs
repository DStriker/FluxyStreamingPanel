using Fluxy.Core.Tickets.Enums;

namespace Fluxy.Core.Tickets.Outcomes
{
    /// <summary>
    /// How writing one message into a support ticket conversation ended.
    /// </summary>
    /// <remarks>
    /// It is its own outcome rather than a <see cref="TicketOutcome"/> with an extra property because
    /// the identifier a caller needs back is the message's, not the ticket's: the conversation is
    /// already known, and the message is what was created. The shape mirrors
    /// <see cref="TicketOutcome"/> otherwise, errors included.
    /// </remarks>
    public sealed record TicketReplyOutcome
    {
        /// <summary>How the operation ended.</summary>
        public required TicketAction Action { get; init; }

        /// <summary>Conversation the message belongs to, when it was found.</summary>
        public Guid? TicketId { get; init; }

        /// <summary>Message that was written, when one was.</summary>
        public Guid? ReplyId { get; init; }

        /// <summary>Rejected fields, keyed by their C# property name, or null.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
