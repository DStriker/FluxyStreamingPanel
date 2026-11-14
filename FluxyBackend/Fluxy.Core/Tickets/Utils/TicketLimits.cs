namespace Fluxy.Core.Tickets.Utils
{
    /// <summary>
    /// Column widths of a support ticket row and the bounds of the endpoints that read and write
    /// them.
    /// </summary>
    /// <remarks>
    /// The limits live in this layer rather than beside the table configuration so the transport,
    /// the service and the write boundary can judge a value without referencing the data access
    /// assembly, and the configuration uses the same constants, so a column width and the rule that
    /// cuts a value to it cannot drift apart.
    /// </remarks>
    public static class TicketLimits
    {
        /// <summary>Longest subject line a ticket may store.</summary>
        public const int SubjectMaxLength = 200;

        /// <summary>Longest message a reply row may store.</summary>
        public const int MessageMaxLength = 4000;

        /// <summary>Tickets one page holds when the caller names no size.</summary>
        public const int DefaultPageSize = 20;

        /// <summary>Most tickets one page may hold.</summary>
        public const int MaxPageSize = 1000;

        /// <summary>Most characters one search may carry.</summary>
        public const int MaxSearchLength = 200;

        /// <summary>
        /// Most messages one ticket's conversation may hold. The service refuses the message that
        /// would cross this bound rather than growing a thread nobody can read to the end of.
        /// </summary>
        public const int MaxRepliesPerTicket = 500;
    }
}
