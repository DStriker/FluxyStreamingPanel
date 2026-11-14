namespace Fluxy.API.Contracts.Tickets
{
    /// <summary>
    /// The constant codes the ticket endpoints answer with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The codes are snake_case, matching the protocol, and they are the part of the contract a
    /// client branches on. Renaming one breaks every client; rewriting the wording beside it
    /// breaks nothing.
    /// </para>
    /// <para>
    /// <c>invalid_status</c> is deliberately the same code the admin account transitions use: a
    /// reopen of a ticket that is not closed is the same kind of contradiction as an unblock of
    /// an account that is not blocked, and a client that knows one knows both. The three
    /// conversation-specific refusals carry their own words because no other resource can
    /// produce them.
    /// </para>
    /// </remarks>
    public static class TicketResponses
    {
        /// <summary>Code reported once a ticket has been created.</summary>
        public const string CreatedCode = "ticket_created";

        /// <summary>Code reported once a message has been written into a conversation.</summary>
        public const string ReplyCreatedCode = "ticket_reply_created";

        /// <summary>Code reported once a conversation has been marked read by the caller's side.</summary>
        public const string MarkedReadCode = "ticket_marked_read";

        /// <summary>Code reported once a conversation has been marked unread again.</summary>
        public const string MarkedUnreadCode = "ticket_marked_unread";

        /// <summary>Code reported once a conversation has been closed.</summary>
        public const string ClosedCode = "ticket_closed";

        /// <summary>Code reported once a conversation has been reopened.</summary>
        public const string ReopenedCode = "ticket_reopened";

        /// <summary>Code reported once a conversation has been deleted.</summary>
        public const string DeletedCode = "ticket_deleted";

        /// <summary>Code reported when no ticket with that identifier is visible to the caller.</summary>
        public const string NotFoundCode = "ticket_not_found";

        /// <summary>Code reported when a closed conversation is asked to accept a message.</summary>
        public const string IsClosedCode = "ticket_is_closed";

        /// <summary>Code reported when a conversation already holds its maximum number of messages.</summary>
        public const string ReplyLimitReachedCode = "ticket_reply_limit_reached";

        /// <summary>Code reported when the asked-for transition does not apply to the lifecycle.</summary>
        public const string InvalidStatusCode = "invalid_status";
    }
}
