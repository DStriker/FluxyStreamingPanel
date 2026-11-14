using Fluxy.Core.Tickets.Commands;
using Fluxy.Core.Tickets.Enums;
using Fluxy.Core.Tickets.Outcomes;
using Fluxy.Core.Tickets.ReadModels;
using Fluxy.Core.Users.Enums;

namespace Fluxy.Core.Tickets.Interfaces
{
    /// <summary>
    /// Opens support tickets, reads them for either side of the conversation, answers them, marks
    /// them read, closes or reopens them, and removes one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One contract serves both entrances, because both sides of a conversation ask the same
    /// questions.</b> The account reads its own tickets and the operator reads the ones opened by
    /// accounts it may see; both read one thread the same way and both answer it. The difference is
    /// scope, and scope is an argument - the account's identifier for the self reads, the caller's
    /// permissions for the operator's - not a second interface.
    /// </para>
    /// <para>
    /// <b>The service owns the rules, the repository the query.</b> Which flag a message raises,
    /// which side may answer, when a message crosses the thread bound and which identifier a caller
    /// is allowed to see all live here; the predicate, the computed last-reply instant, the order
    /// and the page cut live in the repository.
    /// </para>
    /// <para>
    /// The interface says nothing about HTTP, codes or cookies: which outcome becomes a 200, a 201,
    /// a 404 or a 409 is the transport layer's business.
    /// </para>
    /// </remarks>
    public interface ITicketService
    {
        /// <summary>
        /// Reads one filtered page of one account's own tickets, counted before paging.
        /// </summary>
        /// <param name="userId">Account whose tickets are read.</param>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many tickets one page holds.</param>
        /// <param name="search">Case-insensitive substring of the subject, or null.</param>
        /// <param name="status"><c>unread</c>, <c>read</c> or <c>closed</c>, or null for every ticket.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<TicketPage> GetPageAsync(
            Guid userId,
            int page,
            int pageSize,
            string? search,
            string? status,
            TicketSortField sortBy,
            TicketSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads one filtered page of the tickets opened by accounts an operator may read.
        /// </summary>
        /// <param name="granted">Permissions the caller's group holds.</param>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many tickets one page holds.</param>
        /// <param name="search">Case-insensitive substring of the subject or account name, or null.</param>
        /// <param name="status"><c>unread</c>, <c>read</c> or <c>closed</c>, or null for every ticket.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// Which accounts an operator may see is <c>UserPermissionCatalog.VisibleRoles</c>, applied
        /// before the count so the pager never advertises tickets the caller was not going to be
        /// shown.
        /// </remarks>
        Task<AdminTicketPage> GetAdminPageAsync(
            IReadOnlySet<UserPermission> granted,
            int page,
            int pageSize,
            string? search,
            string? status,
            TicketSortField sortBy,
            TicketSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads one ticket with its conversation for one of its two sides, or says why there is
        /// nothing to show.
        /// </summary>
        /// <param name="ticketId">Ticket to read.</param>
        /// <param name="viewerId">Account reading the thread.</param>
        /// <param name="isAdminView">
        /// Whether the reader is the operator side of the conversation. The account's own read is
        /// refused for a ticket it did not open - answered as not found rather than as a permission
        /// problem naming a row the caller was not shown.
        /// </param>
        /// <param name="granted">Permissions the caller's group holds; consulted only on the operator side.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// The operator's read is refused with <see cref="TicketAction.PermissionDenied"/> for a
        /// ticket opened by an account whose role its grants do not reach, and that is a different
        /// answer from a missing row: the operator is being told what stands between them and a
        /// conversation they can see named in no list of theirs.
        /// </remarks>
        Task<TicketReadResult> GetDetailAsync(
            Guid ticketId,
            Guid viewerId,
            bool isAdminView,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens a ticket for one account.
        /// </summary>
        /// <param name="ticket">Account and subject line.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// <para>
        /// The subject is trimmed, and a blank or over-long one is refused through
        /// <see cref="TicketOutcome.Errors"/> keyed by the C# property name instead of being
        /// silently cut: the subject is what the client typed and a stored value shorter than the
        /// one it saw would be a lie about what it sent.
        /// </para>
        /// <para>
        /// <b>An operator is refused.</b> Support is the account's door: an administrator answers
        /// tickets rather than opening one, so a create that arrived through the self-service
        /// entrance with an administrator's token is answered
        /// <see cref="TicketAction.PermissionDenied"/> rather than creating a conversation nobody
        /// asked to read.
        /// </para>
        /// </remarks>
        Task<TicketOutcome> CreateAsync(
            NewTicket ticket,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes one message into an existing conversation.
        /// </summary>
        /// <param name="reply">Conversation, author, side and text of the message.</param>
        /// <param name="granted">Permissions the caller's group holds; consulted only on the operator side.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// A message on a closed ticket is refused with
        /// <see cref="TicketAction.ReplyToClosed"/> and writes nothing, and a message that would
        /// cross <c>TicketLimits.MaxRepliesPerTicket</c> is refused with
        /// <see cref="TicketAction.ReplyLimitReached"/> before it is staged. The message raises the
        /// other side's unread flag and clears the writer's own, because the writer has plainly
        /// read what they just wrote.
        /// </remarks>
        Task<TicketReplyOutcome> ReplyAsync(
            NewTicketReply reply,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks one conversation read by the side that asked, and stamps the counterpart messages
        /// it had not read yet.
        /// </summary>
        /// <param name="ticketId">Ticket to mark.</param>
        /// <param name="viewerId">Account asking.</param>
        /// <param name="isAdminView">Whether the caller is the operator side.</param>
        /// <param name="granted">Permissions the caller's group holds; consulted only on the operator side.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// <see cref="TicketAction.MarkedRead"/>, <see cref="TicketAction.NotFound"/> for a ticket
        /// the caller may not see, or <see cref="TicketAction.PermissionDenied"/> for an operator
        /// whose grants do not reach the account behind it.
        /// </returns>
        /// <remarks>
        /// <b>Idempotent.</b> The operation clears the acting side's own flag and stamps
        /// <c>read_at</c> on every message of the other side that had none; a second call finds
        /// nothing to change and answers the same word. The two sides stamp opposite message sets:
        /// the account reading stamps the operator's messages, the operator reading stamps the
        /// account's.
        /// </remarks>
        Task<TicketOutcome> MarkReadAsync(
            Guid ticketId,
            Guid viewerId,
            bool isAdminView,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks one conversation unread for the operator again.
        /// </summary>
        /// <param name="ticketId">Ticket to mark.</param>
        /// <param name="granted">Permissions the caller's group holds.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// <see cref="TicketAction.MarkedUnread"/>, <see cref="TicketAction.NotFound"/> or
        /// <see cref="TicketAction.PermissionDenied"/>.
        /// </returns>
        /// <remarks>
        /// <b>Operator-only, and that is why it takes no viewer identifier.</b> The account's side
        /// has no unread control in the contract: its flag is raised by an answer, and a visitor who
        /// wants to read a conversation again has nothing to record. Raising the operator's flag
        /// also clears the read mark of the latest message the account wrote, so the row the
        /// operator is about to read again is drawn as new rather than as already read.
        /// </remarks>
        Task<TicketOutcome> MarkUnreadAsync(
            Guid ticketId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes one conversation.
        /// </summary>
        /// <param name="ticketId">Ticket to close.</param>
        /// <param name="granted">Permissions the caller's group holds.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// Closing an already closed ticket is not an error: the second press of a button that
        /// already did its job answers <see cref="TicketAction.Closed"/> with the same identifier.
        /// The operator's unread flag is cleared with the lifecycle, because there is nothing left
        /// to answer; the account's flag is left standing, because closing says nothing about what
        /// the account has read.
        /// </remarks>
        Task<TicketOutcome> CloseAsync(
            Guid ticketId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens one closed conversation again.
        /// </summary>
        /// <param name="ticketId">Ticket to reopen.</param>
        /// <param name="granted">Permissions the caller's group holds.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// <see cref="TicketAction.Reopened"/>, <see cref="TicketAction.InvalidStatus"/> when the
        /// ticket is not closed, <see cref="TicketAction.NotFound"/> or
        /// <see cref="TicketAction.PermissionDenied"/>.
        /// </returns>
        /// <remarks>
        /// Refused rather than answered like the close above when the ticket is not closed: where a
        /// reopen <i>goes</i> is the open lifecycle, and a ticket that is already there has no
        /// answer to give. Reporting success would mean claiming a state change that did not
        /// happen, and the caller is expected to re-read the row.
        /// </remarks>
        Task<TicketOutcome> ReopenAsync(
            Guid ticketId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes one ticket and its whole conversation.
        /// </summary>
        /// <param name="ticketId">Ticket to remove.</param>
        /// <param name="granted">Permissions the caller's group holds.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// <see cref="TicketAction.Deleted"/>, <see cref="TicketAction.NotFound"/> or
        /// <see cref="TicketAction.PermissionDenied"/>.
        /// </returns>
        /// <remarks>
        /// The messages are removed by the database's cascade in the same save, so a ticket whose
        /// conversation survived would not be a state this operation can leave behind. A second
        /// delete is a 404, because the row the caller named is gone.
        /// </remarks>
        Task<TicketOutcome> DeleteAsync(
            Guid ticketId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default);
    }
}
