using Fluxy.Core.Tickets.Commands;
using Fluxy.Core.Tickets.Enums;
using Fluxy.Core.Tickets.Models;
using Fluxy.Core.Tickets.ReadModels;
using Fluxy.Core.Users.Enums;

namespace Fluxy.Core.Tickets.Repositories
{
    /// <summary>
    /// Reads support tickets and their conversations, stages the rows a write creates or changes,
    /// and answers the counts a service's rules read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The repository owns the query shape.</b> The predicate every filter contributes, the
    /// count taken under that same predicate, the computed <c>lastReplyAt</c>, the order with its
    /// tie-break and the page cut all live in one place, so <c>total</c> can never describe a
    /// different set from the page it sits beside. The service owns the rules - who may read a
    /// conversation, when a ticket may be answered, when a message crosses the thread bound - and
    /// nothing about SQL.
    /// </para>
    /// <para>
    /// <b>Writes are staged, never saved.</b> Every method that creates or changes a row hands it to
    /// the tracker; the service owns the single save, so the caller that opens a ticket or answers
    /// one decides the transaction boundary.
    /// </para>
    /// <para>
    /// <b>The status filter is viewer-relative.</b> <c>unread</c> and <c>read</c> are read against
    /// the unread flag of the side whose page is being cut - <see cref="GetPageAsync"/> for the
    /// account's own list and <see cref="GetAdminPageAsync"/> for the operator's - while
    /// <c>closed</c> names the lifecycle both sides share. The repository receives the side through
    /// the method it was called on rather than guessing it, which is why one method serves each
    /// entrance instead of one method with a viewer argument.
    /// </para>
    /// <para>
    /// A read never leaves an <c>IQueryable</c> behind: every method materializes and answers a Core
    /// model or read model, so nothing outside the data access assembly can name the persistence
    /// model or reshape the query after the count was taken.
    /// </para>
    /// </remarks>
    public interface ITicketRepository
    {
        /// <summary>
        /// Reads one filtered page of one account's tickets, counted before paging.
        /// </summary>
        /// <param name="userId">Account whose tickets are read. Rows of anyone else are never in scope.</param>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many tickets one page holds.</param>
        /// <param name="search">Case-insensitive substring of the subject, or null.</param>
        /// <param name="status">
        /// <c>unread</c>, <c>read</c> or <c>closed</c> as resolved by <c>TicketQuery</c>, or null for
        /// every ticket. The first two are read against the account's own unread flag.
        /// </param>
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
        /// Reads one filtered page of the tickets opened by accounts of the roles an operator may
        /// read, counted after that scope is applied and before paging.
        /// </summary>
        /// <param name="visibleRoles">
        /// Roles whose accounts the operator may read, resolved by the service from the caller's
        /// permissions. An empty set matches nothing rather than everything.
        /// </param>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many tickets one page holds.</param>
        /// <param name="search">
        /// Case-insensitive substring of the subject or of the account's current login name, or
        /// null.
        /// </param>
        /// <param name="status">
        /// <c>unread</c>, <c>read</c> or <c>closed</c> as resolved by <c>TicketQuery</c>, or null for
        /// every ticket. The first two are read against the operator's unread flag.
        /// </param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<AdminTicketPage> GetAdminPageAsync(
            IReadOnlySet<UserRole> visibleRoles,
            int page,
            int pageSize,
            string? search,
            string? status,
            TicketSortField sortBy,
            TicketSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads one ticket's row without its conversation, or null when no row carries the
        /// identifier.
        /// </summary>
        /// <param name="ticketId">Ticket to read.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// The transitions of a ticket - close, reopen, the two read marks, a reply - need the
        /// lifecycle and the two flags of one row and nothing of its thread, so they read this
        /// instead of the detail. Ownership and role visibility stay the service's rules.
        /// </remarks>
        Task<Ticket?> GetAsync(Guid ticketId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads one ticket with its whole conversation, oldest message first, or null when no row
        /// carries the identifier.
        /// </summary>
        /// <param name="ticketId">Ticket to read.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// Ownership and role visibility are the service's rules: this method answers the row and
        /// its messages, and the caller that must not show them checks that before answering the
        /// client. The account's name and each author's name are resolved through the users table
        /// on read, never stored.
        /// </remarks>
        Task<TicketDetail?> GetDetailAsync(
            Guid ticketId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages one ticket, left for the caller's single save.
        /// </summary>
        /// <param name="ticket">Account and subject line.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Identifier of the staged ticket.</returns>
        /// <remarks>
        /// The ticket is staged open and with the operator's unread flag raised: a new
        /// conversation is something the operator has not read yet, while the account that just
        /// opened it has. No message is staged with it - the create endpoint carries a subject and
        /// nothing else, and the first message is written into the thread it opens.
        /// </remarks>
        Task<Guid> AddAsync(NewTicket ticket, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages one message of a conversation, left for the caller's single save.
        /// </summary>
        /// <param name="reply">Conversation, author, side and text of the message.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Identifier of the staged message.</returns>
        Task<Guid> AddReplyAsync(
            NewTicketReply reply,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a change of the conversation's lifecycle.
        /// </summary>
        /// <param name="ticketId">Ticket to change.</param>
        /// <param name="status">Lifecycle the ticket is set to.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Whether a row carried the identifier; false stages nothing.</returns>
        Task<bool> SetStatusAsync(
            Guid ticketId,
            TicketStatus status,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages the operator's unread flag of one conversation.
        /// </summary>
        /// <param name="ticketId">Ticket to change.</param>
        /// <param name="unread">Whether the operator has something new to read.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Whether a row carried the identifier; false stages nothing.</returns>
        Task<bool> SetAdminUnreadAsync(
            Guid ticketId,
            bool unread,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages the account's unread flag of one conversation.
        /// </summary>
        /// <param name="ticketId">Ticket to change.</param>
        /// <param name="unread">Whether the account has something new to read.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Whether a row carried the identifier; false stages nothing.</returns>
        Task<bool> SetUserUnreadAsync(
            Guid ticketId,
            bool unread,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stamps a read mark over every message one side wrote that has not been read yet.
        /// </summary>
        /// <param name="ticketId">Conversation whose messages are marked.</param>
        /// <param name="isAdminReply">
        /// Which side wrote the messages being marked. The account reading a thread marks the
        /// operator's messages (<c>true</c>); the operator marks the account's (<c>false</c>).
        /// </param>
        /// <param name="readAt">Moment the marks are stamped with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>How many messages were stamped; zero when there was nothing new.</returns>
        /// <remarks>
        /// The rows are loaded through the tracker and left for the caller's single save, so the
        /// mark and the flag the service clears beside it commit together. A load-free update would
        /// be immediate and outside the unit of work, which this staged write must not be.
        /// </remarks>
        Task<int> MarkRepliesReadAsync(
            Guid ticketId,
            bool isAdminReply,
            DateTimeOffset readAt,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts the messages one conversation holds.
        /// </summary>
        /// <param name="ticketId">Conversation whose messages are counted.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// The service's bound on a thread's length - <c>TicketLimits.MaxRepliesPerTicket</c> - asks
        /// this before staging another message, so the rule is stated once even though the bound
        /// belongs to the domain.
        /// </remarks>
        Task<int> CountRepliesAsync(
            Guid ticketId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages the removal of a ticket and, through the cascade, its whole conversation.
        /// </summary>
        /// <param name="ticketId">Ticket to remove.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Whether a row carried the identifier; false stages nothing.</returns>
        /// <remarks>
        /// The messages go with the row through the <c>ON DELETE CASCADE</c> on
        /// <c>ticket_replies.ticket_id</c>, so one intent removes the conversation. The caller's
        /// single save is the transaction: a ticket that survives because the commit failed is
        /// the state the caller asked to leave behind.
        /// </remarks>
        Task<bool> RemoveAsync(Guid ticketId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages the per-message read mark of one side's newest message back to unread.
        /// </summary>
        /// <param name="ticketId">Conversation whose newest message is marked.</param>
        /// <param name="isAdminReply">
        /// Which side wrote the message being marked. The operator marking a conversation unread
        /// clears the mark of the newest account message (<c>false</c>), because that is the one
        /// the operator is about to read again.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Whether a message was found and staged; false leaves everything untouched.</returns>
        /// <remarks>
        /// Loaded through the tracker and left for the caller's save, so the mark and the flag the
        /// service raises beside it commit together. Only the latest message is cleared: raising
        /// the operator's flag means "there is something new here", and marking the whole thread
        /// unread again would claim more than that.
        /// </remarks>
        Task<bool> MarkLatestReplyUnreadAsync(
            Guid ticketId,
            bool isAdminReply,
            CancellationToken cancellationToken = default);
    }
}
