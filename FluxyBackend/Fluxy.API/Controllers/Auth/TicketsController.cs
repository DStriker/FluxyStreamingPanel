using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Tickets;
using Fluxy.API.Controllers.Support;
using Fluxy.Core.Tickets.Commands;
using Fluxy.Core.Tickets.Enums;
using Fluxy.Core.Tickets.Interfaces;
using Fluxy.Core.Tickets.ReadModels;
using Fluxy.Core.Tickets.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Auth
{
    /// <summary>
    /// The account's own support tickets: its queue, one conversation, and the two writes it owns
    /// - opening a ticket and answering it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The account comes from the token and only from the token.</b> There is no
    /// <c>userId</c> in any body or query, so a caller can only ever read and answer its own
    /// conversations; a ticket identifier belonging to somebody else is answered
    /// <c>ticket_not_found</c> rather than <c>permission_denied</c>, because the identifier is a
    /// uuid and telling the caller that a row exists would turn the route into a probe for other
    /// accounts' conversations.
    /// </para>
    /// <para>
    /// <b>This controller has no close, reopen, delete or unread, and that absence is the
    /// fence.</b> The account answers its conversation; declaring the end of it and removing it
    /// are the operator's. A <c>DELETE {id}</c>, a <c>POST {id}/close</c> or a
    /// <c>POST {id}/unread</c> declared now would be a route with no rule behind it, so those
    /// paths are not declared at all and answer <c>405 method_not_allowed</c> through the shared
    /// status-page envelope.
    /// </para>
    /// <para>
    /// <b>An operator is refused the create.</b> Support is the account's door, and the refusal is
    /// the service's rather than this controller's - a second endpoint reaching the create must
    /// not be able to forget it.
    /// </para>
    /// <para>
    /// Every read carries <c>Cache-Control: no-store</c> through <see cref="ApiControllerBase.NoStore"/>:
    /// a page of one account's conversations is not something a shared cache may keep.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("auth/tickets")]
    [Authorize]
    [Produces("application/json")]
    public sealed class TicketsController : ApiControllerBase
    {
        private readonly ITicketService _tickets;

        /// <summary>
        /// Initializes a new instance of the <see cref="TicketsController"/> class.
        /// </summary>
        /// <param name="tickets">Service that reads and changes the conversations.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public TicketsController(
            ITicketService tickets,
            IAntiforgery antiforgery,
            ILogger<TicketsController> logger)
            : base(antiforgery, logger)
        {
            _tickets = tickets;
        }

        /// <summary>
        /// Reports one page of the caller's own conversations.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many tickets one page holds.</param>
        /// <param name="search">Case-insensitive substring of the subject, or null.</param>
        /// <param name="status">Viewer-relative state to keep: <c>unread</c>, <c>read</c> or <c>closed</c>.</param>
        /// <param name="sortBy">Column to order by; <c>lastReplyAt</c> when absent.</param>
        /// <param name="sortOrder">Direction; <c>desc</c> when absent.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with one page, or 400 naming the parameters that were broken.</returns>
        /// <remarks>
        /// <c>username</c> is deliberately not a sort field here: every row belongs to the same
        /// account, so ordering by its name would order nothing.
        /// </remarks>
        [HttpGet]
        [ProducesResponseType<TicketPageResponse>(StatusCodes.Status200OK)]
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

            if (User.UserId() is not { } userId)
            {
                return ApiResults.Anonymous();
            }

            var list = await _tickets.GetPageAsync(
                userId,
                query!.Page,
                query.PageSize,
                query.Search,
                query.Status,
                query.SortBy,
                query.SortOrder,
                cancellationToken);

            return Ok(new TicketPageResponse
            {
                Items = list.Items.Select(ToItem).ToList(),
                Total = list.Total,
                Page = list.Page,
                PageSize = list.PageSize
            });
        }

        /// <summary>
        /// Reports one of the caller's own conversations in full.
        /// </summary>
        /// <param name="id">Identifier of the ticket.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 with the ticket and its thread, 404 <c>ticket_not_found</c> for an identifier that
        /// names no ticket or somebody else's.
        /// </returns>
        /// <remarks>
        /// One answer for both absences on purpose - see the class remarks.
        /// </remarks>
        [HttpGet("{id:guid}")]
        [ProducesResponseType<TicketDetailResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTicket(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            NoStore();

            if (User.UserId() is not { } userId)
            {
                return ApiResults.Anonymous();
            }

            var result = await _tickets.GetDetailAsync(
                id,
                userId,
                isAdminView: false,
                NoGrants,
                cancellationToken);

            if (result.Detail is not { } detail)
            {
                return NotFoundAnswer();
            }

            var lastReplyAt = detail.Replies.Count == 0
                ? detail.CreatedAt
                : detail.Replies.Max(reply => reply.CreatedAt);

            return Ok(new TicketDetailResponse
            {
                Id = detail.Id.ToString(),
                Subject = detail.Subject,
                Status = TicketStatuses.ForUser(detail.Status, detail.UserUnread),
                CreatedAt = detail.CreatedAt,
                UpdatedAt = detail.UpdatedAt,
                LastReplyAt = lastReplyAt,
                Replies = detail.Replies.Select(ToReply).ToList()
            });
        }

        /// <summary>
        /// Opens a ticket for the caller.
        /// </summary>
        /// <param name="request">The subject line.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 201 <c>ticket_created</c> with a <c>Location</c> naming the new conversation,
        /// 400 with the rejected field, 403 <c>permission_denied</c> for an operator,
        /// 400 <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// The 201 carries a <c>Location</c>, unlike the create endpoint that has no addressable
        /// row: this ticket exists in full the moment it is written, this very controller reads it
        /// back, and the frontend takes the visitor straight into the conversation. It is a
        /// relative reference, because the server does not know which host serves the frontend and
        /// building an absolute address from a guess is how an open redirect gets introduced.
        /// </remarks>
        [HttpPost]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateTicket(
            [FromBody] CreateTicketRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (User.UserId() is not { } userId)
            {
                return ApiResults.Anonymous();
            }

            var outcome = await _tickets.CreateAsync(
                new NewTicket
                {
                    UserId = userId,
                    Subject = request.Subject ?? string.Empty
                },
                cancellationToken);

            if (outcome.Action is TicketAction.Created && outcome.TicketId is { } ticketId)
            {
                Response.Headers.Location = $"/auth/tickets/{ticketId}";
            }

            return Answer(outcome);
        }

        /// <summary>
        /// Answers one of the caller's own conversations.
        /// </summary>
        /// <param name="id">Identifier of the ticket.</param>
        /// <param name="request">The message.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 201 <c>ticket_reply_created</c>, 404 <c>ticket_not_found</c>, 409
        /// <c>ticket_is_closed</c> or <c>ticket_reply_limit_reached</c>, 400 for an empty or
        /// oversized message or a missing antiforgery pair.
        /// </returns>
        /// <remarks>
        /// The side is this entrance's, not the body's: a self-service reply is always the
        /// account's, and a body naming the side would let a caller write as somebody else.
        /// </remarks>
        [HttpPost("{id:guid}/replies")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
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

            if (User.UserId() is not { } userId)
            {
                return ApiResults.Anonymous();
            }

            var outcome = await _tickets.ReplyAsync(
                new NewTicketReply
                {
                    TicketId = id,
                    AuthorId = userId,
                    Message = request.Message ?? string.Empty,
                    IsAdminReply = false
                },
                NoGrants,
                cancellationToken);

            return Answer(outcome);
        }

        /// <summary>
        /// Marks one of the caller's own conversations read, stamping the operator's messages that
        /// had no read mark yet.
        /// </summary>
        /// <param name="id">Identifier of the ticket.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>ticket_marked_read</c>, 404 <c>ticket_not_found</c>, 400 <c>csrf_invalid</c>
        /// without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// Idempotent: a second call finds the account's flag already clear and no unmarked
        /// operator message, and answers the same word.
        /// </remarks>
        [HttpPost("{id:guid}/read")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MarkRead(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (User.UserId() is not { } userId)
            {
                return ApiResults.Anonymous();
            }

            return Answer(await _tickets.MarkReadAsync(
                id,
                userId,
                isAdminView: false,
                NoGrants,
                cancellationToken));
        }

        /// <summary>
        /// Answers <c>405</c> to a <c>DELETE {id}</c>, which the account does not own.
        /// </summary>
        /// <param name="id">Identifier in the path; deliberately never read.</param>
        /// <returns>405 <c>method_not_allowed</c>.</returns>
        /// <remarks>
        /// <b>The fence is the absence of a handler, and this declares only that absence.</b>
        /// Removing a conversation is the operator's capability; a <c>{id}</c> path that named no
        /// route would answer the generic <c>404</c>, and the contract answers it with
        /// <c>405</c> instead. Nothing here reads the identifier, and nothing is written.
        /// </remarks>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status405MethodNotAllowed)]
        public IActionResult RejectDelete([FromRoute] string id)
            => MethodNotAllowed(id);

        /// <summary>
        /// Answers <c>405</c> to <c>POST {id}/close</c>, which the account does not own.
        /// </summary>
        /// <param name="id">Identifier in the path; deliberately never read.</param>
        /// <returns>405 <c>method_not_allowed</c>.</returns>
        [HttpPost("{id:guid}/close")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status405MethodNotAllowed)]
        public IActionResult RejectClose([FromRoute] string id)
            => MethodNotAllowed(id);

        /// <summary>
        /// Answers <c>405</c> to <c>POST {id}/reopen</c>, which the account does not own.
        /// </summary>
        /// <param name="id">Identifier in the path; deliberately never read.</param>
        /// <returns>405 <c>method_not_allowed</c>.</returns>
        [HttpPost("{id:guid}/reopen")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status405MethodNotAllowed)]
        public IActionResult RejectReopen([FromRoute] string id)
            => MethodNotAllowed(id);

        /// <summary>
        /// Answers <c>405</c> to <c>POST {id}/unread</c>, which only the operator may call.
        /// </summary>
        /// <param name="id">Identifier in the path; deliberately never read.</param>
        /// <returns>405 <c>method_not_allowed</c>.</returns>
        /// <remarks>
        /// The account's unread flag is raised by an answer, not by a button, so there is no
        /// self-service control to give a body to. The operator's route exists on the other
        /// controller; this one names the same path on this entrance and refuses it.
        /// </remarks>
        [HttpPost("{id:guid}/unread")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status405MethodNotAllowed)]
        public IActionResult RejectUnread([FromRoute] string id)
            => MethodNotAllowed(id);

        /// <summary>The one refusal every fenced method answers with.</summary>
        private static IActionResult MethodNotAllowed(string id)
        {
            _ = id;

            return new ObjectResult(new MessageResponse
            {
                Code = "method_not_allowed",
                Message = "The method is not allowed for this resource."
            })
            {
                StatusCode = StatusCodes.Status405MethodNotAllowed
            };
        }

        /// <summary>
        /// The empty grant set a self-service call passes: the account side is scoped by ownership
        /// rather than by a permission, and the service never consults the set on that side.
        /// </summary>
        private static readonly IReadOnlySet<Core.Users.Enums.UserPermission> NoGrants =
            new HashSet<Core.Users.Enums.UserPermission>();

        /// <summary>The one 404 a self-service read answers with.</summary>
        private static IActionResult NotFoundAnswer()
            => new ObjectResult(new MessageResponse
            {
                Code = TicketResponses.NotFoundCode,
                Message = "There is no ticket with that identifier."
            })
            {
                StatusCode = StatusCodes.Status404NotFound
            };

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

        private static TicketItemResponse ToItem(TicketListItem item) => new()
        {
            Id = item.Id.ToString(),
            Subject = item.Subject,
            Status = TicketStatuses.ForUser(item.Status, item.UserUnread),
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
