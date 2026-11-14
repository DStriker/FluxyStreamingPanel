namespace Fluxy.Core.Tickets.Enums
{
    /// <summary>
    /// Lifecycle of a support ticket: whether the conversation is still being answered or the
    /// operator has declared it finished.
    /// </summary>
    /// <remarks>
    /// <b>The numbers are the schema.</b> They are written into <c>tickets.status</c> as a
    /// <c>smallint</c>, so renumbering a member would reinterpret every stored row as a different
    /// state without a single query being made. The wire does not use these numbers: a client reads
    /// a derived word (<c>closed</c>, <c>unread</c>, <c>read</c>) that also accounts for which side
    /// of the conversation is asking.
    /// </remarks>
    public enum TicketStatus
    {
        /// <summary>The conversation is still open.</summary>
        Open = 1,

        /// <summary>The conversation was closed by the account or the operator.</summary>
        Closed = 2
    }
}
