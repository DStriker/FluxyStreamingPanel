namespace Fluxy.Core.Tickets.Enums
{
    /// <summary>
    /// How an operation on a support ticket ended.
    /// </summary>
    /// <remarks>
    /// <b>The service knows only these words.</b> Which of them becomes a 201, a 204, a 404 or a
    /// 409 is the transport's answer, stated beside the endpoint's response table - so a status
    /// code can change without touching a rule, and a rule can change without anybody having to
    /// decide what status code it should start returning. The members are not persisted and their
    /// numbers carry no meaning.
    /// </remarks>
    public enum TicketAction
    {
        /// <summary>The ticket and its first message were created.</summary>
        Created = 0,

        /// <summary>A message was added to the conversation.</summary>
        Replied = 1,

        /// <summary>The ticket was closed.</summary>
        Closed = 2,

        /// <summary>A closed ticket was opened again.</summary>
        Reopened = 3,

        /// <summary>No ticket with that identifier is visible to the caller.</summary>
        NotFound = 4,

        /// <summary>One of the values did not pass validation; which one is in the errors.</summary>
        Invalid = 5,

        /// <summary>The conversation was marked read by the side that asked.</summary>
        MarkedRead = 6,

        /// <summary>The operator's unread flag was raised again.</summary>
        MarkedUnread = 7,

        /// <summary>The ticket and its whole conversation were deleted.</summary>
        Deleted = 8,

        /// <summary>
        /// The caller may not touch this conversation: the account that opened it belongs to a
        /// role its group's grants do not reach.
        /// </summary>
        PermissionDenied = 9,

        /// <summary>
        /// The action does not apply to the lifecycle the ticket is in - reopening a ticket that
        /// is not closed.
        /// </summary>
        InvalidStatus = 10,

        /// <summary>The conversation is closed, so a message cannot be written into it.</summary>
        ReplyToClosed = 11,

        /// <summary>The conversation already holds the maximum number of messages.</summary>
        ReplyLimitReached = 12
    }
}
