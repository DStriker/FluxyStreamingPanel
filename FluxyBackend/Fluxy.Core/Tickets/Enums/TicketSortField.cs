namespace Fluxy.Core.Tickets.Enums
{
    /// <summary>
    /// The columns a page of support tickets may be ordered by.
    /// </summary>
    /// <remarks>
    /// <b>Every member is a value the repository can order by before the page is cut.</b>
    /// <see cref="LastReplyAt"/> is computed from the messages of the ticket with a fallback to the
    /// ticket's own creation instant, so a ticket nobody has answered yet is still placed by when
    /// it arrived. <see cref="Username"/> is resolved through the users table on read - no name is
    /// snapshotted - and only the admin list draws it.
    /// </remarks>
    public enum TicketSortField
    {
        /// <summary>Subject line of the ticket.</summary>
        Subject = 0,

        /// <summary>Login name of the account that opened the ticket.</summary>
        Username = 1,

        /// <summary>When the ticket was opened.</summary>
        CreatedAt = 2,

        /// <summary>
        /// When the conversation last moved: the newest message, or the ticket's own creation
        /// instant when none exists. The default, and the newest first.
        /// </summary>
        LastReplyAt = 3
    }
}
