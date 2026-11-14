using Fluxy.Core.Tickets.Commands;
using Fluxy.Core.Tickets.Enums;
using Fluxy.Core.Tickets.Models;
using Fluxy.Core.Tickets.ReadModels;
using Fluxy.Core.Tickets.Repositories;
using Fluxy.Core.Tickets.Utils;
using Fluxy.Core.Users.Enums;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="ITicketRepository"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The page is one query over the tickets, joined once to the account that opened them.</b>
    /// The join resolves the operator's list name and the role scope; from the ticket side the
    /// relation is one account per ticket, so it multiplies no row and the count under the same
    /// predicate answers the same set the page is cut from.
    /// </para>
    /// <para>
    /// <b><c>total</c> is counted under the same predicate the page is cut from, before
    /// <c>Skip</c>/<c>Take</c>.</b> Counting after paging would describe the page instead of the
    /// list and make the pager lie. On the operator's list the role scope is part of that predicate,
    /// so the count never advertises tickets the caller was not going to be shown.
    /// </para>
    /// <para>
    /// <b><c>lastReplyAt</c> is computed, never stored.</b> It is the newest message's creation
    /// instant with the ticket's own instant as the fallback, so a ticket nobody has answered is
    /// still placed by when it arrived. The same expression fills the row and is what the default
    /// order reads, which is the only way the order and the drawn value can stay one fact.
    /// </para>
    /// </remarks>
    internal sealed class TicketRepository : ITicketRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="TicketRepository"/> class.
        /// </summary>
        /// <param name="context">Context the rows are read from and staged in.</param>
        public TicketRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<TicketPage> GetPageAsync(
            Guid userId,
            int page,
            int pageSize,
            string? search,
            string? status,
            TicketSortField sortBy,
            TicketSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            var query =
                from ticket in _context.Tickets.AsNoTracking()
                join user in _context.Users.AsNoTracking() on ticket.UserId equals user.Id
                where ticket.UserId == userId
                select new TicketRow
                {
                    Id = ticket.Id,
                    UserId = ticket.UserId,
                    Username = user.Username,
                    Subject = ticket.Subject,
                    Status = ticket.Status,
                    AdminUnread = ticket.AdminUnread,
                    UserUnread = ticket.UserUnread,
                    CreatedAt = ticket.CreatedAt,

                    // The navigation carries the correlation EF needs: one subquery per ticket over
                    // its own messages, coalesced to the ticket's own instant when none exists.
                    LastReplyAt = ticket.Replies
                        .Max(reply => (DateTimeOffset?)reply.CreatedAt) ?? ticket.CreatedAt
                };

            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                // The subject is the stored free text this list can search. The account list is one
                // account's rows, so its own name adds nothing to a search and is left out.
                query = query.Where(row => row.Subject.ToLower().Contains(needle));
            }

            // The status filter is viewer-relative: `unread` and `read` are this account's own
            // flag, and a closed ticket reads as closed whatever that flag says, so both exclude
            // it. `closed` is the stored lifecycle and means the same thing to both sides.
            if (status == TicketStatuses.Unread)
            {
                query = query.Where(row =>
                    row.Status != TicketStatus.Closed && row.UserUnread);
            }
            else if (status == TicketStatuses.Read)
            {
                query = query.Where(row =>
                    row.Status != TicketStatus.Closed && !row.UserUnread);
            }
            else if (status == TicketStatuses.Closed)
            {
                query = query.Where(row => row.Status == TicketStatus.Closed);
            }

            // Counted under the same predicate the page is cut from, and before the order is
            // applied: a total counted after Skip/Take would describe the page, not the list.
            var total = await query.CountAsync(cancellationToken);

            var rows = await Order(query, sortBy, sortOrder)
                // Arithmetic in long: page comes from a query string, so (page - 1) * pageSize can
                // overflow int and reach EF Core as a negative Skip.
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new TicketPage
            {
                Items = rows
                    .Select(row => new TicketListItem
                    {
                        Id = row.Id,
                        Subject = row.Subject,
                        Status = row.Status,
                        UserUnread = row.UserUnread,
                        CreatedAt = row.CreatedAt,
                        LastReplyAt = row.LastReplyAt
                    })
                    .ToList(),
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public async Task<AdminTicketPage> GetAdminPageAsync(
            IReadOnlySet<UserRole> visibleRoles,
            int page,
            int pageSize,
            string? search,
            string? status,
            TicketSortField sortBy,
            TicketSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            // The join is the scope: an operator may read the tickets of the accounts whose group
            // role it may read, and the inner join simply matches nothing for the rest. The scope
            // is part of the query rather than a filter over the returned rows, because the count
            // must describe the same set the page is cut from.
            var query =
                from ticket in _context.Tickets.AsNoTracking()
                join user in _context.Users.AsNoTracking() on ticket.UserId equals user.Id
                where visibleRoles.Contains(user.Group!.Role)
                select new TicketRow
                {
                    Id = ticket.Id,
                    UserId = ticket.UserId,
                    Username = user.Username,
                    Subject = ticket.Subject,
                    Status = ticket.Status,
                    AdminUnread = ticket.AdminUnread,
                    UserUnread = ticket.UserUnread,
                    CreatedAt = ticket.CreatedAt,

                    // Same computed column as the self list: the navigation keeps the correlation
                    // inside the page's own query, so no second round trip is needed.
                    LastReplyAt = ticket.Replies
                        .Max(reply => (DateTimeOffset?)reply.CreatedAt) ?? ticket.CreatedAt
                };

            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                // The operator's search covers the subject and the account's current name, the two
                // texts the row draws - the name is resolved by the join above, not stored.
                query = query.Where(row =>
                    row.Subject.ToLower().Contains(needle)
                    || row.Username.ToLower().Contains(needle));
            }

            // The status filter is viewer-relative: `unread` and `read` are the operator's own flag,
            // and both exclude a closed ticket because closing ends the conversation whatever the
            // flag left standing says.
            if (status == TicketStatuses.Unread)
            {
                query = query.Where(row =>
                    row.Status != TicketStatus.Closed && row.AdminUnread);
            }
            else if (status == TicketStatuses.Read)
            {
                query = query.Where(row =>
                    row.Status != TicketStatus.Closed && !row.AdminUnread);
            }
            else if (status == TicketStatuses.Closed)
            {
                query = query.Where(row => row.Status == TicketStatus.Closed);
            }

            var total = await query.CountAsync(cancellationToken);

            var rows = await Order(query, sortBy, sortOrder)
                // Same long arithmetic as the self list: the offset is built from a query string.
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new AdminTicketPage
            {
                Items = rows
                    .Select(row => new AdminTicketListItem
                    {
                        Id = row.Id,
                        UserId = row.UserId,
                        Username = row.Username,
                        Subject = row.Subject,
                        Status = row.Status,
                        AdminUnread = row.AdminUnread,
                        CreatedAt = row.CreatedAt,
                        LastReplyAt = row.LastReplyAt
                    })
                    .ToList(),
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public async Task<Ticket?> GetAsync(
            Guid ticketId,
            CancellationToken cancellationToken = default)
        {
            // The row alone: a transition needs the lifecycle and the two flags, and reading the
            // thread beside them would be a second query for values nothing in the rule reads.
            var row = await _context.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);

            return row?.ToModel();
        }

        /// <inheritdoc />
        public async Task<TicketDetail?> GetDetailAsync(
            Guid ticketId,
            CancellationToken cancellationToken = default)
        {
            var ticket = await (
                    from entry in _context.Tickets.AsNoTracking()
                    join user in _context.Users.AsNoTracking() on entry.UserId equals user.Id
                    where entry.Id == ticketId
                    select new
                    {
                        entry.Id,
                        entry.UserId,
                        Username = user.Username,
                        entry.Subject,
                        entry.Status,
                        entry.AdminUnread,
                        entry.UserUnread,
                        entry.CreatedAt,
                        entry.UpdatedAt
                    })
                .FirstOrDefaultAsync(cancellationToken);

            if (ticket is null)
            {
                return null;
            }

            // The thread is oldest first, with the identifier as the tie-break: two messages can be
            // written in the same instant, and without it a page drawn twice could reorder them.
            // The author is a left join: deleting the account keeps the message and leaves the name
            // null, which is exactly what the SET NULL foreign key says.
            var replies = await (
                    from reply in _context.TicketReplies.AsNoTracking()
                    join account in _context.Users.AsNoTracking()
                        on reply.AuthorId equals (Guid?)account.Id into authors
                    from author in authors.DefaultIfEmpty()
                    where reply.TicketId == ticketId
                    orderby reply.CreatedAt, reply.Id
                    select new TicketReplyItem
                    {
                        Id = reply.Id,
                        AuthorId = reply.AuthorId,
                        AuthorUsername = author == null ? null : author.Username,
                        Message = reply.Message,
                        IsAdminReply = reply.IsAdminReply,
                        ReadAt = reply.ReadAt,
                        CreatedAt = reply.CreatedAt
                    })
                .ToListAsync(cancellationToken);

            return new TicketDetail
            {
                Id = ticket.Id,
                UserId = ticket.UserId,
                Username = ticket.Username,
                Subject = ticket.Subject,
                Status = ticket.Status,
                AdminUnread = ticket.AdminUnread,
                UserUnread = ticket.UserUnread,
                CreatedAt = ticket.CreatedAt,
                UpdatedAt = ticket.UpdatedAt,
                Replies = replies
            };
        }

        /// <inheritdoc />
        public async Task<Guid> AddAsync(
            NewTicket ticket,
            CancellationToken cancellationToken = default)
        {
            // Staged, not saved: the caller owns the single save. The row starts open with the
            // operator's unread flag raised - a new conversation is something the operator has not
            // read yet - and the account's own flag clear, because the account wrote the subject
            // and has nothing waiting for it.
            var row = new TicketEntity
            {
                UserId = ticket.UserId,
                Subject = ticket.Subject,
                Status = TicketStatus.Open,
                AdminUnread = true,
                UserUnread = false
            };

            await _context.Tickets.AddAsync(row, cancellationToken);

            return row.Id;
        }

        /// <inheritdoc />
        public async Task<Guid> AddReplyAsync(
            NewTicketReply reply,
            CancellationToken cancellationToken = default)
        {
            var row = new TicketReplyEntity
            {
                TicketId = reply.TicketId,
                AuthorId = reply.AuthorId,
                Message = reply.Message,
                IsAdminReply = reply.IsAdminReply
            };

            await _context.TicketReplies.AddAsync(row, cancellationToken);

            return row.Id;
        }

        /// <inheritdoc />
        public async Task<bool> SetStatusAsync(
            Guid ticketId,
            TicketStatus status,
            CancellationToken cancellationToken = default)
        {
            var row = await _context.Tickets.FindAsync([ticketId], cancellationToken);

            if (row is null)
            {
                return false;
            }

            row.Status = status;

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> SetAdminUnreadAsync(
            Guid ticketId,
            bool unread,
            CancellationToken cancellationToken = default)
        {
            var row = await _context.Tickets.FindAsync([ticketId], cancellationToken);

            if (row is null)
            {
                return false;
            }

            row.AdminUnread = unread;

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> SetUserUnreadAsync(
            Guid ticketId,
            bool unread,
            CancellationToken cancellationToken = default)
        {
            var row = await _context.Tickets.FindAsync([ticketId], cancellationToken);

            if (row is null)
            {
                return false;
            }

            row.UserUnread = unread;

            return true;
        }

        /// <inheritdoc />
        public Task<int> MarkRepliesReadAsync(
            Guid ticketId,
            bool isAdminReply,
            DateTimeOffset readAt,
            CancellationToken cancellationToken = default)
        {
            // Loaded through the tracker rather than updated in place: the mark is part of the
            // same business operation as the flag the service clears beside it, so it must commit
            // in the caller's single save - the rule every repository here follows. The set is one
            // conversation's unread messages, bounded by the thread limit, so the read is small.
            return MarkTrackedAsync(ticketId, isAdminReply, readAt, cancellationToken);
        }

        /// <inheritdoc />
        public Task<int> CountRepliesAsync(
            Guid ticketId,
            CancellationToken cancellationToken = default)
            => _context.TicketReplies
                .AsNoTracking()
                .CountAsync(reply => reply.TicketId == ticketId, cancellationToken);

        /// <inheritdoc />
        public async Task<bool> RemoveAsync(
            Guid ticketId,
            CancellationToken cancellationToken = default)
        {
            var row = await _context.Tickets.FindAsync([ticketId], cancellationToken);

            if (row is null)
            {
                return false;
            }

            // Staged, not executed: the messages go with the row through the cascade in the same
            // save, so a commit that fails leaves both halves where they were.
            _context.Tickets.Remove(row);

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> MarkLatestReplyUnreadAsync(
            Guid ticketId,
            bool isAdminReply,
            CancellationToken cancellationToken = default)
        {
            // The newest message of the named side, tracked so the clear commits with the flag the
            // service raises beside it. The identifier is the tie-break of the thread's own order,
            // so two messages written in one instant still have one "latest".
            var row = await _context.TicketReplies
                .Where(reply =>
                    reply.TicketId == ticketId
                    && reply.IsAdminReply == isAdminReply)
                .OrderByDescending(reply => reply.CreatedAt)
                .ThenByDescending(reply => reply.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (row is null)
            {
                return false;
            }

            row.ReadAt = null;

            return true;
        }

        /// <summary>
        /// Stages the read mark over one side's unread messages and reports how many it touched.
        /// </summary>
        /// <remarks>
        /// The rows are tracked so the context stamps <c>updated_at</c> with the rest of the save;
        /// a load-free update would sit outside the unit of work, which is the one thing a staged
        /// mark may not do.
        /// </remarks>
        private async Task<int> MarkTrackedAsync(
            Guid ticketId,
            bool isAdminReply,
            DateTimeOffset readAt,
            CancellationToken cancellationToken)
        {
            var rows = await _context.TicketReplies
                .Where(reply =>
                    reply.TicketId == ticketId
                    && reply.IsAdminReply == isAdminReply
                    && reply.ReadAt == null)
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                row.ReadAt = readAt;
            }

            return rows.Count;
        }

        /// <summary>
        /// Orders a page by the requested column, with the identifier as the tie-break in the same
        /// direction.
        /// </summary>
        /// <remarks>
        /// Every order ends with the identifier: two tickets can share a subject, an account name or
        /// an instant, and without the tie-break the database is free to answer a different order on
        /// every read - which would make a page turn show a row twice or skip one. The default is
        /// <c>lastReplyAt</c>, the column the list is meant to open on.
        /// </remarks>
        private static IQueryable<TicketRow> Order(
            IQueryable<TicketRow> query,
            TicketSortField sortBy,
            TicketSortOrder sortOrder)
        {
            var descending = sortOrder == TicketSortOrder.Descending;

            var ordered = sortBy switch
            {
                TicketSortField.Subject => descending
                    ? query.OrderByDescending(row => row.Subject)
                    : query.OrderBy(row => row.Subject),
                TicketSortField.Username => descending
                    ? query.OrderByDescending(row => row.Username)
                    : query.OrderBy(row => row.Username),
                TicketSortField.CreatedAt => descending
                    ? query.OrderByDescending(row => row.CreatedAt)
                    : query.OrderBy(row => row.CreatedAt),
                _ => descending
                    ? query.OrderByDescending(row => row.LastReplyAt)
                    : query.OrderBy(row => row.LastReplyAt)
            };

            return descending
                ? ordered.ThenByDescending(row => row.Id)
                : ordered.ThenBy(row => row.Id);
        }

        /// <summary>
        /// One row of either list, before it is turned into the read model the client receives.
        /// </summary>
        /// <remarks>
        /// Both lists need the same columns and differ only in which unread flag they draw and
        /// filter by, so one row shape carries both flags and each method reads its own. It never
        /// leaves this class and is not a Core type: it is a projection, not a contract.
        /// </remarks>
        private sealed class TicketRow
        {
            public required Guid Id { get; init; }

            public required Guid UserId { get; init; }

            public required string Username { get; init; }

            public required string Subject { get; init; }

            public required TicketStatus Status { get; init; }

            public required bool AdminUnread { get; init; }

            public required bool UserUnread { get; init; }

            public required DateTimeOffset CreatedAt { get; init; }

            public required DateTimeOffset LastReplyAt { get; init; }
        }
    }
}
