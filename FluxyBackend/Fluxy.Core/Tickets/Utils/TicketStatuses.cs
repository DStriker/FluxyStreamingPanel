using Fluxy.Core.Tickets.Enums;

namespace Fluxy.Core.Tickets.Utils
{
    /// <summary>
    /// The words a support ticket's status is answered with, and the rule that decides which one a
    /// given viewer sees.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The stored lifecycle and the wire word are two different facts.</b> The row stores
    /// whether the ticket is open or closed; a client reads <c>closed</c>, <c>unread</c> or
    /// <c>read</c>, which is relative to the side of the conversation that is asking. Putting the
    /// derivation here keeps one answer for both entrances: the account's list and the operator's
    /// list cannot disagree about what "unread" means.
    /// </para>
    /// <para>
    /// <b>A closed ticket always reads as closed.</b> Closing ends the conversation, so a flag left
    /// standing by the message that closed it must not keep drawing the row as something waiting
    /// for an answer. The lifecycle is checked first, and only an open ticket asks the viewer's own
    /// flag.
    /// </para>
    /// <para>
    /// <b>The two sides are independent.</b> A message the account wrote raises the operator's flag
    /// and says nothing about the account's; <see cref="ForAdmin"/> and <see cref="ForUser"/> take
    /// the one flag that belongs to the side being drawn, so a screen cannot accidentally read the
    /// other side's state.
    /// </para>
    /// </remarks>
    public static class TicketStatuses
    {
        /// <summary>Word a closed conversation reads as, whatever either unread flag says.</summary>
        public const string Closed = "closed";

        /// <summary>Word an open conversation reads as when the viewer has something new.</summary>
        public const string Unread = "unread";

        /// <summary>Word an open conversation reads as when the viewer has nothing new.</summary>
        public const string Read = "read";

        /// <summary>Every word a status filter or a response row may carry.</summary>
        public static IReadOnlyList<string> All { get; } = [Unread, Read, Closed];

        /// <summary>
        /// Turns the stored lifecycle and the viewer's own unread flag into the word a client
        /// reads.
        /// </summary>
        /// <param name="status">Stored lifecycle of the conversation.</param>
        /// <param name="unreadForViewer">
        /// Whether the side being drawn has a message it has not read yet.
        /// </param>
        /// <returns><c>closed</c> when the ticket is closed, otherwise <c>unread</c> or <c>read</c>.</returns>
        public static string ForViewer(TicketStatus status, bool unreadForViewer) =>
            status == TicketStatus.Closed
                ? Closed
                : unreadForViewer
                    ? Unread
                    : Read;

        /// <summary>
        /// The word the operator's screen reads: the lifecycle and the operator's own unread flag.
        /// </summary>
        /// <param name="status">Stored lifecycle of the conversation.</param>
        /// <param name="adminUnread">Whether the operator has a message it has not read yet.</param>
        public static string ForAdmin(TicketStatus status, bool adminUnread) =>
            ForViewer(status, adminUnread);

        /// <summary>
        /// The word the account's screen reads: the lifecycle and that account's own unread flag.
        /// </summary>
        /// <param name="status">Stored lifecycle of the conversation.</param>
        /// <param name="userUnread">Whether the account has a message it has not read yet.</param>
        public static string ForUser(TicketStatus status, bool userUnread) =>
            ForViewer(status, userUnread);

        /// <summary>
        /// Whether a word is one a status filter may name.
        /// </summary>
        /// <param name="value">Word as typed, any case.</param>
        /// <returns>True when it names <c>unread</c>, <c>read</c> or <c>closed</c>.</returns>
        public static bool IsKnown(string? value) =>
            value is not null && All.Contains(value.Trim().ToLowerInvariant());
    }
}
