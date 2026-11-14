namespace Fluxy.Core.Tickets.Commands
{
    /// <summary>
    /// Values one message of a support ticket conversation is written with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="IsAdminReply"/> names the side, not the author's current role.</b> The service
    /// that accepts a message knows which entrance the request came through, and the stored fact is
    /// which side of the conversation the message belongs to - an operator account that later
    /// changes role does not move its old messages to the other side.
    /// </para>
    /// <para>
    /// The identifier and the audit timestamps are the row's and the context's, as everywhere else.
    /// </para>
    /// </remarks>
    public sealed record NewTicketReply
    {
        /// <summary>Ticket the message belongs to.</summary>
        public required Guid TicketId { get; init; }

        /// <summary>Account that writes the message.</summary>
        public required Guid AuthorId { get; init; }

        /// <summary>Message body as the author typed it, before trimming or truncation.</summary>
        public required string Message { get; init; }

        /// <summary>Whether the operator is writing the message rather than the account.</summary>
        public required bool IsAdminReply { get; init; }
    }
}
