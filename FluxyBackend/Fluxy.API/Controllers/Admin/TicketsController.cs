using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Tickets;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.API.Security;
using Fluxy.Core.Tickets.Enums;
using Fluxy.Core.Tickets.Interfaces;
using Fluxy.Core.Tickets.ReadModels;
using Fluxy.Core.Tickets.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Admin
{
    /// <summary>
    /// The operator's side of the support tickets: every conversation the caller's grants reach,
    /// one thread in full, and the state transitions only an operator may make.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The transitions are RPC-shaped <c>POST {id}/…</c> actions, which is a recorded
    /// departure.</b> <c>read</c>, <c>unread</c>, <c>close</c> and <c>reopen</c> are operations
    /// with rules of their own - idempotency, which flag they clear, whether the lifecycle admits
    /// them - rather than fields of a row a client may patch. A <c>PATCH {id}</c> with a state
    /// would carry those rules in the caller's payload, and <c>PUT</c>/<c>DELETE</c> cannot mean
    /// "mark read". The alternative a state field would produce is the same one the account
    /// transitions already rejected: every client re-implementing the rule, and the first one to
    /// skip it corrupting the row.
    /// </para>
    /// <para>
    /// <b>Administrators never create a ticket, and that absence is the fence.</b> Support is the
    /// account's door; an operator answers conversations rather than opening one. There is no
    /// <c>POST /admin/tickets</c>, so the collection answers <c>405 method_not_allowed</c> to that
    /// method through the shared status-page envelope - proven at the wire by an integration test
    /// rather than by reading the controller.
    /// </para>
    /// <para>
    /// <b>The writes ride the account write grants, which is the second departure.</b> Answering,
    /// closing, reopening and deleting a conversation change what the account's surface holds, so
    /// they need <c>editClients|editResellers|editAdmins</c>; the reads need the corresponding
    /// <c>view*</c> trio. A dedicated ticket permission pair was rejected because it would be a
    /// new <c>UserPermission</c> member past the enum's int ceiling, and because reading a
    /// client's conversation is the same question as reading the client. The policies are coarse;
    /// <see cref="ITicketService"/> narrows them to the row's role.
    /// </para>
    /// <para>
    /// <b>Every parameter is parsed as plain text, case-insensitively.</b> Enum binding takes a
    /// member name spelled in full, so <c>sortOrder=desc</c> would be refused while
    /// <c>sortOrder=Descending</c> was accepted. The mapping lives in
    /// <see cref="TicketQuery"/>, in Core, so every rule about a value that names nothing is
    /// testable without a running host; this action keeps the HTTP half.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/tickets")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class TicketsController : ApiControllerBase
    {
        private readonly ITicketService _tickets;
        private readonly RequestStandingReader _standing;

        /// <summary>
        /// Initializes a new instance of the <see cref="TicketsController"/> class.
        /// </summary>
        /// <param name="tickets">Service that reads and changes the conversations.</param>
        /// <param name="standing">
        /// Reader of the account behind the request, shared with the authorization handlers, so
        /// the grants the per-row scope needs cost no second query.
        /// </param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the transitions worth remembering are reported to.</param>
        public TicketsController(
            ITicketService tickets,
            RequestStandingReader standing,
            IAntiforgery antiforgery,
            ILogger<TicketsController> logger)
            : base(antiforgery, logger)
        {
            _tickets = tickets;
            _standing = standing;
        }

        /// <summary>
        /// Reports one page of the conversations this operator may read, filtered and ordered the
        /// way the query asked.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many tickets one page holds.</param>
        /// <param name="search">Case-insensitive substring of the subject or the account name, or null.</param>
        /// <param name="status">Viewer-relative state to keep: <c>unread</c>, <c>read</c> or <c>closed</c>.</param>
        /// <param name="sortBy">Column to order by; <c>lastReplyAt</c> when absent.</param>
        /// <param name="sortOrder">Direction; <c>desc</c> when absent.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with one page, or 400 naming the parameters that were broken.</returns>
        /// <remarks>
        /// <para>
        /// <b>The role scope is applied before the count.</b> An operator holding
        /// <c>viewClients</c> only is shown the clients' conversations and a total that agrees
        /// with them - a page whose rows were never going to be shown is a worse answer than an
        /// empty one, and a pager built from a wider total is worse still.
        /// </para>
        /// <para>
        /// <b><c>status</c> is not a sort field.</b> The wire word is derived from the lifecycle
        /// and the viewer's own flag, so it is not a column the page can be ordered by before it
        /// is cut; naming it where a sort field belongs is refused, not silently answered with
        /// the default order.
        /// </para>
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewTicketsPolicy)]
        [ProducesResponseType<AdminTicketPageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetTickets(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] string? sortBy = "lastReplyAt",
            [FromQuery] string? sortOrder = "desc",
            CancellationToken cancellationToken = default)
        {
            NoStore();

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            MergeListErrors(
                errors,
                ListQueryErrors.Common(
                    page ?? 1,
                    pageSize ?? TicketLimits.DefaultPageSize,
                    search,
                    TicketLimits.MaxPageSize,
                    TicketLimits.MaxSearchLength));

            var query = TicketQuery.TryParse(
                page,
                pageSize,
                search,
                status,
                sortBy,
                sortOrder,
                out var queryErrors);

            MergeListErrors(errors, queryErrors);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var list = await _tickets.GetAdminPageAsync(
                await _standing.ReadGrantsAsync(User),
                query!.Page,
                query.PageSize,
                query.Search,
                query.Status,
                query.SortBy,
                query.SortOrder,
                cancellationToken);

            return Ok(new AdminTicketPageResponse
            {
                Items = list.Items.Select(ToItem).ToList(),
                Total = list.Total,
                Page = list.Page,
                PageSize = list.PageSize
            });
        }

        /// <summary>
        /// Reports one conversation in full, with its whole thread.
        /// </summary>
        /// <param name="id">Identifier of the ticket.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 with the ticket, 404 <c>ticket_not_found</c> when no row carries the identifier,
        /// 403 <c>permission_denied</c> when the account behind it is of a role this operator's
        /// grants do not reach.
        /// </returns>
        /// <remarks>
        /// A missing ticket and an identifier belonging to nobody are the same 404: the identifier
        /// is a uuid, so "not found" is the only thing that can be wrong with it. An existing
        /// conversation whose account is out of scope is answered 403 rather than folded into that
        /// 404, because the operator may already have seen the row in a list a colleague's wider
        /// grant showed them, and calling it non-existent would contradict it.
        /// </remarks>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.ViewTicketsPolicy)]
        [ProducesResponseType<AdminTicketDetailResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTicket(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            NoStore();

            var result = await _tickets.GetDetailAsync(
                id,
                User.UserId() ?? Guid.Empty,
                isAdminView: true,
                await _standing.ReadGrantsAsync(User),
                cancellationToken);

            if (result.Detail is not { } detail)
            {
                return Answer(new Core.Tickets.Outcomes.TicketOutcome
                {
                    Action = result.Action,
                    TicketId = result.Action is TicketAction.NotFound ? null : id
                });
            }

            var lastReplyAt = detail.Replies.Count == 0
                ? detail.CreatedAt
                : detail.Replies.Max(reply => reply.CreatedAt);

            return Ok(new AdminTicketDetailResponse
            {
                Id = detail.Id.ToString(),
                UserId = detail.UserId.ToString(),
                Username = detail.Username,
                Subject = detail.Subject,
                Status = TicketStatuses.ForAdmin(detail.Status, detail.AdminUnread),
                CreatedAt = detail.CreatedAt,
                UpdatedAt = detail.UpdatedAt,
                LastReplyAt = lastReplyAt,
                Replies = detail.Replies.Select(ToReply).ToList()
            });
        }

        /// <summary>
        /// Writes one message into a conversation as the operator.
        /// </summary>
        /// <param name="id">Identifier of the ticket.</param>
        /// <param name="request">The message.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 201 <c>ticket_reply_created</c> without a <c>Location</c>, 404 <c>ticket_not_found</c>,
        /// 403 <c>permission_denied</c>, 409 <c>ticket_is_closed</c> or
        /// <c>ticket_reply_limit_reached</c>, 400 for an empty or oversized message or a missing
        /// antiforgery pair.
        /// </returns>
        /// <remarks>
        /// <b>No <c>Location</c>, deliberately.</b> The row created is a message, and this surface
        /// has no address that names one: the thread is read whole through its ticket, and a
        /// message URI nobody can GET would be a header pointing at a 404. RFC 9110 makes the
        /// header a SHOULD, and this is the case it leaves open - the same trade the registration
        /// endpoint records.
        /// </remarks>
        [HttpPost("{id:guid}/replies")]
        [Authorize(Policy = AuthenticationExtensions.EditTicketsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Reply(
            [FromRoute] Guid id,
            [FromBody] CreateTicketReplyRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (User.UserId() is not { } authorId)
            {
                return ApiResults.Anonymous();
            }

            var outcome = await _tickets.ReplyAsync(
                new Core.Tickets.Commands.NewTicketReply
                {
                    TicketId = id,
                    AuthorId = authorId,
                    Message = request.Message ?? string.Empty,
                    IsAdminReply = true
                },
                await _standing.ReadGrantsAsync(User),
                cancellationToken);

            return Answer(outcome);
        }

        /// <summary>
        /// Marks one conversation read for this operator, stamping the account messages that had
        /// no read mark yet.
        /// </summary>
        /// <param name="id">Identifier of the ticket.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>ticket_marked_read</c>, 404 <c>ticket_not_found</c>, 403 <c>permission_denied</c>,
        /// 400 <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// Idempotent: a second call finds the flag already clear and no unmarked message, and
        /// answers the same word. The mark runs over the account's messages, because those are the
        /// ones this side reads.
        /// </remarks>
        [HttpPost("{id:guid}/read")]
        [Authorize(Policy = AuthenticationExtensions.EditTicketsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MarkRead(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (User.UserId() is not { } viewerId)
            {
                return ApiResults.Anonymous();
            }

            return Answer(await _tickets.MarkReadAsync(
                id,
                viewerId,
                isAdminView: true,
                await _standing.ReadGrantsAsync(User),
                cancellationToken));
        }

        /// <summary>
        /// Marks one conversation unread for this operator again, clearing the read mark of the
        /// latest message the account wrote.
        /// </summary>
        /// <param name="id">Identifier of the ticket.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>ticket_marked_unread</c>, 404 <c>ticket_not_found</c>, 403 <c>permission_denied</c>,
        /// 400 <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// Idempotent, and only the latest account message is unmarked: raising the operator's flag
        /// means "there is something new here", not "the whole thread is unread again".
        /// </remarks>
        [HttpPost("{id:guid}/unread")]
        [Authorize(Policy = AuthenticationExtensions.EditTicketsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MarkUnread(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await _tickets.MarkUnreadAsync(
                id,
                await _standing.ReadGrantsAsync(User),
                cancellationToken));
        }

        /// <summary>
        /// Closes one conversation.
        /// </summary>
        /// <param name="id">Identifier of the ticket.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>ticket_closed</c>, 404 <c>ticket_not_found</c>, 403 <c>permission_denied</c>,
        /// 400 <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// Idempotent on purpose: closing an already closed ticket answers the same word, because
        /// the state the caller asked for is the state that holds.
        /// </remarks>
        [HttpPost("{id:guid}/close")]
        [Authorize(Policy = AuthenticationExtensions.EditTicketsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Close(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await _tickets.CloseAsync(
                id,
                await _standing.ReadGrantsAsync(User),
                cancellationToken));
        }

        /// <summary>
        /// Opens one closed conversation again.
        /// </summary>
        /// <param name="id">Identifier of the ticket.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>ticket_reopened</c>, 409 <c>invalid_status</c> when the ticket is not closed,
        /// 404 <c>ticket_not_found</c>, 403 <c>permission_denied</c>, 400 <c>csrf_invalid</c>
        /// without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// Refused rather than answered like the close when the ticket is already open: where a
        /// reopen goes is the open lifecycle, and a ticket already there has no state change to
        /// report. The caller is expected to re-read the row.
        /// </remarks>
        [HttpPost("{id:guid}/reopen")]
        [Authorize(Policy = AuthenticationExtensions.EditTicketsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Reopen(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await _tickets.ReopenAsync(
                id,
                await _standing.ReadGrantsAsync(User),
                cancellationToken));
        }

        /// <summary>
        /// Deletes one ticket and its whole conversation.
        /// </summary>
        /// <param name="id">Identifier of the ticket.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>ticket_deleted</c>, 404 <c>ticket_not_found</c> for a ticket already gone,
        /// 403 <c>permission_denied</c>, 400 <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// A <c>DELETE</c> that answers 200 rather than 204, because it has something to say: the
        /// caller has to know the difference between a conversation that was deleted and one that
        /// was not there to delete, and an empty body cannot say it. The messages go with the row
        /// through the cascade in the same save, and a second delete answers 404.
        /// </remarks>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditTicketsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await _tickets.DeleteAsync(
                id,
                await _standing.ReadGrantsAsync(User),
                cancellationToken));
        }

        /// <summary>Turns a ticket outcome into the status and body a client is told.</summary>
        private static IActionResult Answer(Core.Tickets.Outcomes.TicketOutcome outcome)
        {
            var (status, body) = TicketOutcomeResponses.Describe(outcome);

            return new ObjectResult(body) { StatusCode = status };
        }

        /// <summary>Turns a message outcome into the status and body a client is told.</summary>
        private static IActionResult Answer(Core.Tickets.Outcomes.TicketReplyOutcome outcome)
        {
            var (status, body) = TicketOutcomeResponses.Describe(outcome);

            return new ObjectResult(body) { StatusCode = status };
        }

        /// <summary>Adds every entry of a refusal map to the one this action answers with.</summary>
        private static void MergeListErrors(
            Dictionary<string, string[]> target,
            Dictionary<string, string[]> source)
        {
            foreach (var entry in source)
            {
                target[entry.Key] = entry.Value;
            }
        }

        private static AdminTicketItemResponse ToItem(AdminTicketListItem item) => new()
        {
            Id = item.Id.ToString(),
            UserId = item.UserId.ToString(),
            Username = item.Username,
            Subject = item.Subject,
            Status = TicketStatuses.ForAdmin(item.Status, item.AdminUnread),
            CreatedAt = item.CreatedAt,
            LastReplyAt = item.LastReplyAt
        };

        private static TicketReplyResponse ToReply(TicketReplyItem reply) => new()
        {
            Id = reply.Id.ToString(),
            Message = reply.Message,
            IsAdminReply = reply.IsAdminReply,
            AuthorId = reply.AuthorId?.ToString(),
            AuthorName = reply.AuthorUsername,
            ReadAt = reply.ReadAt,
            CreatedAt = reply.CreatedAt
        };
    }
}
