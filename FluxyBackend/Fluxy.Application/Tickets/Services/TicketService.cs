using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Tickets.Commands;
using Fluxy.Core.Tickets.Enums;
using Fluxy.Core.Tickets.Interfaces;
using Fluxy.Core.Tickets.Models;
using Fluxy.Core.Tickets.Outcomes;
using Fluxy.Core.Tickets.ReadModels;
using Fluxy.Core.Tickets.Repositories;
using Fluxy.Core.Tickets.Utils;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Repositories;
using Fluxy.Core.Users.Utils;

namespace Fluxy.Application.Tickets.Services
{
    /// <summary>
    /// The support ticket rules: who may open a conversation, who may read one, which flag a
    /// message raises, when a thread refuses another message, and which transition a lifecycle
    /// admits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The service decides, the repository queries.</b> The predicate of a page, the computed
    /// <c>lastReplyAt</c>, the order with its tie-break and the page cut all live in
    /// <see cref="ITicketRepository"/>; what lives here is every rule that has an answer: which
    /// side of a conversation a caller is on, whether it may see the row at all, which of the two
    /// unread flags a message raises and clears, and when a message crosses the thread bound.
    /// </para>
    /// <para>
    /// <b>One save per operation.</b> Every repository call stages; the single
    /// <see cref="IUnitOfWork.SaveChangesAsync"/> at the end of a successful operation is the
    /// transaction boundary, so a ticket and its flags, or a message and the flag it raises,
    /// cannot commit apart.
    /// </para>
    /// <para>
    /// <b>The wire status is derived here, never stored.</b> A row stores the lifecycle and the
    /// two flags; <see cref="TicketStatuses"/> turns the pair the viewer owns into
    /// <c>unread</c>, <c>read</c> or <c>closed</c>. Both entrances therefore answer the same word
    /// for the same state, and a filter and a row cannot disagree about what "unread" means.
    /// </para>
    /// </remarks>
    public sealed class TicketService : ITicketService
    {
        private readonly ITicketRepository _tickets;
        private readonly IUserRepository _users;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="TicketService"/> class.
        /// </summary>
        /// <param name="tickets">Repository that stages the rows and answers the reads.</param>
        /// <param name="users">Repository that answers the role behind a ticket's owner.</param>
        /// <param name="unitOfWork">Unit of work every operation commits through.</param>
        /// <param name="timeProvider">Clock the read marks are stamped from.</param>
        public TicketService(
            ITicketRepository tickets,
            IUserRepository users,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider)
        {
            _tickets = tickets;
            _users = users;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
        }

        /// <inheritdoc />
        public Task<TicketPage> GetPageAsync(
            Guid userId,
            int page,
            int pageSize,
            string? search,
            string? status,
            TicketSortField sortBy,
            TicketSortOrder sortOrder,
            CancellationToken cancellationToken = default)
            => _tickets.GetPageAsync(
                userId,
                page,
                pageSize,
                search,
                status,
                sortBy,
                sortOrder,
                cancellationToken);

        /// <inheritdoc />
        public Task<AdminTicketPage> GetAdminPageAsync(
            IReadOnlySet<UserPermission> granted,
            int page,
            int pageSize,
            string? search,
            string? status,
            TicketSortField sortBy,
            TicketSortOrder sortOrder,
            CancellationToken cancellationToken = default)
            // The scope is decided before the count: a page whose rows were never going to be
            // shown is a worse answer than an empty one, and a pager whose total advertises them
            // is worse still.
            => _tickets.GetAdminPageAsync(
                UserPermissionCatalog.VisibleRoles(granted),
                page,
                pageSize,
                search,
                status,
                sortBy,
                sortOrder,
                cancellationToken);

        /// <inheritdoc />
        public async Task<TicketReadResult> GetDetailAsync(
            Guid ticketId,
            Guid viewerId,
            bool isAdminView,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default)
        {
            var detail = await _tickets.GetDetailAsync(ticketId, cancellationToken);

            if (detail is null)
            {
                return TicketReadResult.Refused(TicketAction.NotFound);
            }

            if (!isAdminView)
            {
                // One answer for "no such ticket" and "somebody else's ticket": the identifier is
                // the only thing a self caller can name, and telling it that a row exists would
                // turn the route into a probe for other accounts' conversations.
                return detail.UserId == viewerId
                    ? TicketReadResult.Found(detail)
                    : TicketReadResult.Refused(TicketAction.NotFound);
            }

            var scope = await ScopeAsync(detail.UserId, granted, editing: false, cancellationToken);

            return scope is null
                ? TicketReadResult.Found(detail)
                : TicketReadResult.Refused(scope.Value);
        }

        /// <inheritdoc />
        public async Task<TicketOutcome> CreateAsync(
            NewTicket ticket,
            CancellationToken cancellationToken = default)
        {
            var subject = ticket.Subject?.Trim() ?? string.Empty;

            if (subject.Length is 0)
            {
                return Invalid(
                    nameof(NewTicket.Subject),
                    $"The subject is required and must be at most {TicketLimits.SubjectMaxLength} characters.");
            }

            if (subject.Length > TicketLimits.SubjectMaxLength)
            {
                // Refused rather than cut: the client typed a subject and would be told the ticket
                // was created with one it never saw. A reply's message is cut because it is part
                // of an operation that already happened; a subject is the whole intent.
                return Invalid(
                    nameof(NewTicket.Subject),
                    $"The subject must be at most {TicketLimits.SubjectMaxLength} characters.");
            }

            if (await _users.GetRoleAsync(ticket.UserId, cancellationToken) is not { } role)
            {
                return new TicketOutcome { Action = TicketAction.PermissionDenied };
            }

            if (role is UserRole.Admin)
            {
                // Support is the account's door. An administrator answers tickets rather than
                // opening one, and the refusal belongs here rather than in the controller: a second
                // endpoint reaching this method must not be able to forget it.
                return new TicketOutcome { Action = TicketAction.PermissionDenied };
            }

            var ticketId = await _tickets.AddAsync(
                new NewTicket { UserId = ticket.UserId, Subject = subject },
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new TicketOutcome
            {
                Action = TicketAction.Created,
                TicketId = ticketId
            };
        }

        /// <inheritdoc />
        public async Task<TicketReplyOutcome> ReplyAsync(
            NewTicketReply reply,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default)
        {
            var message = reply.Message?.Trim() ?? string.Empty;

            if (message.Length is 0)
            {
                return new TicketReplyOutcome
                {
                    Action = TicketAction.Invalid,
                    TicketId = reply.TicketId,
                    Errors = Errors(
                        nameof(NewTicketReply.Message),
                        $"The message is required and must be at most {TicketLimits.MessageMaxLength} characters.")
                };
            }

            if (message.Length > TicketLimits.MessageMaxLength)
            {
                return new TicketReplyOutcome
                {
                    Action = TicketAction.Invalid,
                    TicketId = reply.TicketId,
                    Errors = Errors(
                        nameof(NewTicketReply.Message),
                        $"The message must be at most {TicketLimits.MessageMaxLength} characters.")
                };
            }

            var ticket = await _tickets.GetAsync(reply.TicketId, cancellationToken);

            if (ticket is null)
            {
                return new TicketReplyOutcome
                {
                    Action = TicketAction.NotFound,
                    TicketId = reply.TicketId
                };
            }

            // Who the caller is decides which rule admits it: the operator side is scoped by its
            // grants, the account side by the account that opened the conversation. The side is
            // the message's own fact (IsAdminReply), never the author's current role.
            var scope = reply.IsAdminReply
                ? await ScopeAsync(ticket.UserId, granted, editing: true, cancellationToken)
                : ticket.UserId == reply.AuthorId
                    ? null
                    : TicketAction.NotFound;

            if (scope is { } refusal)
            {
                return new TicketReplyOutcome
                {
                    Action = refusal,
                    TicketId = reply.TicketId
                };
            }

            if (ticket.Status is TicketStatus.Closed)
            {
                // Nothing is staged: a refused reply leaves the row exactly where it was.
                return new TicketReplyOutcome
                {
                    Action = TicketAction.ReplyToClosed,
                    TicketId = ticket.Id
                };
            }

            if (await _tickets.CountRepliesAsync(ticket.Id, cancellationToken)
                >= TicketLimits.MaxRepliesPerTicket)
            {
                return new TicketReplyOutcome
                {
                    Action = TicketAction.ReplyLimitReached,
                    TicketId = ticket.Id
                };
            }

            var replyId = await _tickets.AddReplyAsync(
                new NewTicketReply
                {
                    TicketId = ticket.Id,
                    AuthorId = reply.AuthorId,
                    Message = message,
                    IsAdminReply = reply.IsAdminReply
                },
                cancellationToken);

            // The writer has plainly read what they just wrote, and the other side has not. One
            // helper answers which flag a message raises so writing and clearing cannot disagree.
            await _tickets.SetAdminUnreadAsync(
                ticket.Id,
                TicketReplySides.RaisesAdminUnread(reply.IsAdminReply),
                cancellationToken);

            await _tickets.SetUserUnreadAsync(
                ticket.Id,
                TicketReplySides.RaisesUserUnread(reply.IsAdminReply),
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new TicketReplyOutcome
            {
                Action = TicketAction.Replied,
                TicketId = ticket.Id,
                ReplyId = replyId
            };
        }

        /// <inheritdoc />
        public async Task<TicketOutcome> MarkReadAsync(
            Guid ticketId,
            Guid viewerId,
            bool isAdminView,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default)
        {
            var ticket = await _tickets.GetAsync(ticketId, cancellationToken);

            if (ticket is null)
            {
                return new TicketOutcome { Action = TicketAction.NotFound };
            }

            if (!isAdminView)
            {
                return ticket.UserId == viewerId
                    ? await MarkReadAsync(ticket, ticketId, isAdminView: false, cancellationToken)
                    : new TicketOutcome { Action = TicketAction.NotFound };
            }

            if (await ScopeAsync(ticket.UserId, granted, editing: false, cancellationToken) is { } refusal)
            {
                return new TicketOutcome { Action = refusal };
            }

            return await MarkReadAsync(ticket, ticketId, isAdminView: true, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<TicketOutcome> MarkUnreadAsync(
            Guid ticketId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default)
        {
            var ticket = await _tickets.GetAsync(ticketId, cancellationToken);

            if (ticket is null)
            {
                return new TicketOutcome { Action = TicketAction.NotFound };
            }

            if (await ScopeAsync(ticket.UserId, granted, editing: true, cancellationToken) is { } refusal)
            {
                return new TicketOutcome { Action = refusal };
            }

            // Idempotent: a ticket already unread for the operator keeps the word, and the latest
            // account message's mark is cleared again only if it was set. Nothing here depends on
            // where the state started.
            await _tickets.SetAdminUnreadAsync(ticketId, true, cancellationToken);
            await _tickets.MarkLatestReplyUnreadAsync(
                ticketId,
                isAdminReply: false,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new TicketOutcome
            {
                Action = TicketAction.MarkedUnread,
                TicketId = ticketId
            };
        }

        /// <inheritdoc />
        public async Task<TicketOutcome> CloseAsync(
            Guid ticketId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default)
        {
            var ticket = await _tickets.GetAsync(ticketId, cancellationToken);

            if (ticket is null)
            {
                return new TicketOutcome { Action = TicketAction.NotFound };
            }

            if (await ScopeAsync(ticket.UserId, granted, editing: true, cancellationToken) is { } refusal)
            {
                return new TicketOutcome { Action = refusal };
            }

            // Closing an already closed ticket answers the same word: the state the caller asked
            // for is the state that holds. The operator's flag is cleared because there is nothing
            // left to answer; the account's flag is untouched, because closing says nothing about
            // what the account has read.
            await _tickets.SetStatusAsync(ticketId, TicketStatus.Closed, cancellationToken);
            await _tickets.SetAdminUnreadAsync(ticketId, false, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new TicketOutcome
            {
                Action = TicketAction.Closed,
                TicketId = ticketId
            };
        }

        /// <inheritdoc />
        public async Task<TicketOutcome> ReopenAsync(
            Guid ticketId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default)
        {
            var ticket = await _tickets.GetAsync(ticketId, cancellationToken);

            if (ticket is null)
            {
                return new TicketOutcome { Action = TicketAction.NotFound };
            }

            if (await ScopeAsync(ticket.UserId, granted, editing: true, cancellationToken) is { } refusal)
            {
                return new TicketOutcome { Action = refusal };
            }

            if (ticket.Status is not TicketStatus.Closed)
            {
                // Refused rather than answered like the close: where a reopen goes is the open
                // lifecycle, and an open ticket is already there.
                return new TicketOutcome
                {
                    Action = TicketAction.InvalidStatus,
                    TicketId = ticketId
                };
            }

            await _tickets.SetStatusAsync(ticketId, TicketStatus.Open, cancellationToken);
            await _tickets.SetAdminUnreadAsync(ticketId, false, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new TicketOutcome
            {
                Action = TicketAction.Reopened,
                TicketId = ticketId
            };
        }

        /// <inheritdoc />
        public async Task<TicketOutcome> DeleteAsync(
            Guid ticketId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default)
        {
            var ticket = await _tickets.GetAsync(ticketId, cancellationToken);

            if (ticket is null)
            {
                return new TicketOutcome { Action = TicketAction.NotFound };
            }

            if (await ScopeAsync(ticket.UserId, granted, editing: true, cancellationToken) is { } refusal)
            {
                return new TicketOutcome { Action = refusal };
            }

            await _tickets.RemoveAsync(ticketId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new TicketOutcome
            {
                Action = TicketAction.Deleted,
                TicketId = ticketId
            };
        }

        /// <summary>
        /// Marks one conversation read for one side and stamps the counterpart messages that had
        /// no mark yet.
        /// </summary>
        /// <remarks>
        /// The stamp is a fact about the other side's messages, so the set it runs over is decided
        /// by which side is reading: <c>isAdminReply</c> names the side that wrote them, and the
        /// reader is the other one. A second call finds no unmarked message and answers the same
        /// word; the flag it clears was already clear.
        /// </remarks>
        private async Task<TicketOutcome> MarkReadAsync(
            Ticket ticket,
            Guid ticketId,
            bool isAdminView,
            CancellationToken cancellationToken)
        {
            if (isAdminView)
            {
                await _tickets.SetAdminUnreadAsync(ticketId, false, cancellationToken);
            }
            else
            {
                await _tickets.SetUserUnreadAsync(ticketId, false, cancellationToken);
            }

            await _tickets.MarkRepliesReadAsync(
                ticketId,
                isAdminReply: !isAdminView,
                _timeProvider.GetUtcNow(),
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new TicketOutcome
            {
                Action = TicketAction.MarkedRead,
                TicketId = ticket.Id
            };
        }

        /// <summary>
        /// Whether the caller may touch a ticket opened by <paramref name="ownerId"/>, or the
        /// refusal to answer with.
        /// </summary>
        /// <param name="ownerId">Account that opened the conversation.</param>
        /// <param name="granted">Permissions the caller's group holds.</param>
        /// <param name="editing">Whether the operation changes the row rather than reading it.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Null when the row is in scope, otherwise the action to answer with.</returns>
        /// <remarks>
        /// The role of the row is a fact about the account, so it is read rather than inferred from
        /// the policy: the policy settles that this caller may touch accounts of <i>some</i> role,
        /// and this decides which one. A missing account is not found rather than denied - a
        /// conversation whose owner has been deleted was removed with it.
        /// </remarks>
        private async Task<TicketAction?> ScopeAsync(
            Guid ownerId,
            IReadOnlySet<UserPermission> granted,
            bool editing,
            CancellationToken cancellationToken)
        {
            if (await _users.GetRoleAsync(ownerId, cancellationToken) is not { } role)
            {
                return TicketAction.NotFound;
            }

            var allowed = editing
                ? UserPermissionCatalog.CanEdit(granted, role)
                : UserPermissionCatalog.CanView(granted, role);

            return allowed ? null : TicketAction.PermissionDenied;
        }

        /// <summary>An invalid outcome naming one field, keyed by its C# property name.</summary>
        private static TicketOutcome Invalid(string field, string message) => new()
        {
            Action = TicketAction.Invalid,
            Errors = Errors(field, message)
        };

        /// <summary>One rejected field, in the shape every outcome here carries.</summary>
        private static IReadOnlyDictionary<string, string[]> Errors(string field, string message)
            => new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [field] = [message]
            };
    }
}
