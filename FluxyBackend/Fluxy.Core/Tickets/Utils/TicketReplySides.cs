namespace Fluxy.Core.Tickets.Utils
{
    /// <summary>
    /// The two sides of a support ticket conversation, named by the messages that belong to them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A side is a fact of the message, not of the account's current role.</b> The row stores
    /// whether the operator or the account wrote a message, and the ticket's two unread flags are
    /// raised on the opposite side: an account's message is news to the operator, and an operator's
    /// reply is news to the account.
    /// </para>
    /// <para>
    /// The helper exists so the service that writes a flag and the service that clears one cannot
    /// disagree about which flag a message raises - the question is answered once, here.
    /// </para>
    /// </remarks>
    public static class TicketReplySides
    {
        /// <summary>Name of the operator's side of a conversation.</summary>
        public const string Admin = "admin";

        /// <summary>Name of the account's side of a conversation.</summary>
        public const string User = "user";

        /// <summary>
        /// Names the side a message belongs to.
        /// </summary>
        /// <param name="isAdminReply">Whether the operator wrote the message.</param>
        public static string Of(bool isAdminReply) => isAdminReply ? Admin : User;

        /// <summary>
        /// Whether a new message raises the operator's unread flag - true for an account's message.
        /// </summary>
        /// <param name="isAdminReply">Whether the operator wrote the message.</param>
        public static bool RaisesAdminUnread(bool isAdminReply) => !isAdminReply;

        /// <summary>
        /// Whether a new message raises the account's unread flag - true for an operator's reply.
        /// </summary>
        /// <param name="isAdminReply">Whether the operator wrote the message.</param>
        public static bool RaisesUserUnread(bool isAdminReply) => isAdminReply;
    }
}
