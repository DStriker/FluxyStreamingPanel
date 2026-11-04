using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Subscriptions;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Subscriptions.Interfaces;
using Fluxy.Core.Subscriptions.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Subscriptions
{
    /// <summary>
    /// The streaming connection log: what was watched under which subscription, from where, and
    /// for how long.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two endpoints, and deliberately only two.</b> The log is read and it is wiped wholesale;
    /// nothing here creates, edits or ends a connection. The process that relays a stream owns the
    /// write path, and a kill action has no meaning until that process exists to obey one. A
    /// <c>POST</c>, a <c>PATCH</c> or a <c>DELETE {id}</c> declared now would be a route with no
    /// rule behind it.
    /// </para>
    /// <para>
    /// <b>No new permission.</b> Reading the log is reading the subscription surface, so it carries
    /// <c>viewSubscriptions</c>; wiping it destroys the evidence that surface is about, so it
    /// carries <c>editSubscriptions</c> plus a fresh antiforgery pair. A third pair would split one
    /// screen's access into two grants an operator has to reason about, and the owner ratified the
    /// reuse explicitly.
    /// </para>
    /// <para>
    /// <b>Country and provider are stored columns, and the read path never resolves GeoIP.</b> That
    /// is the deliberate divergence from the visit history: a value resolved while a page is being
    /// built cannot be filtered or counted by the server, and this table is filtered by both. The
    /// snapshot is taken when the session opens and kept as it was - it describes that connection,
    /// not the network today. A lookup here would also answer a different value from the one the
    /// filter just ran against.
    /// </para>
    /// <para>
    /// <b>The subscription is an <c>ON DELETE CASCADE</c> foreign key.</b> Deleting a subscription
    /// deletes the connections it carried. That is intended: the log is evidence about a
    /// subscription, and removing the subject is what the operator asked for.
    /// </para>
    /// <para>
    /// <b>Sorting is limited to the two stored timestamps on purpose.</b> The owner's name and the
    /// stream's title are joined and resolved <i>for the page</i> - the title from one of two
    /// tables, keyed by the identifiers the page turned out to carry - so an order built on either
    /// would order a page the server had already cut. That is the same lie a filter applied in the
    /// browser tells, and the same reason the pager is not allowed to lie here.
    /// </para>
    /// <para>
    /// <b>The page size ceiling is 1000, refused rather than clamped.</b> Silently turning a
    /// request for 5000 rows into 1000 would answer with a page whose items do not match the size
    /// the caller was told it got.
    /// </para>
    /// <para>
    /// <b>The table is indexed for the questions this screen asks, and one of the indexes is
    /// written by hand.</b> Six indexes answer the default order, the subscription drill-down, the
    /// stream lookup and the three exact filters; the seventh is a partial index over
    /// <c>(started_at DESC, id DESC) WHERE ended_at IS NULL</c>, because "live" is a small tail of a
    /// table that grows forever. EF Core's model index has no filter, so that one is created in the
    /// migration's <c>Up</c> and dropped in its <c>Down</c> - the same treatment the subscriptions'
    /// partial unique indexes get, and the reason a model change must be scaffolded rather than
    /// hand-edited.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/connection-logs")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class ConnectionLogsController : ApiControllerBase
    {
        private readonly IConnectionLogService _logs;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionLogsController"/> class.
        /// </summary>
        /// <param name="logs">Service that reads and clears the log.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public ConnectionLogsController(
            IConnectionLogService logs,
            IAntiforgery antiforgery,
            ILogger<ConnectionLogsController> logger)
            : base(antiforgery, logger)
        {
            _logs = logs;
        }

        /// <summary>
        /// Reports one page of the log, filtered and ordered the way the query asked.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many connections one page holds.</param>
        /// <param name="search">Substring of the address, user agent or provider, or null.</param>
        /// <param name="kind">Medium to keep - <c>video</c> when absent.</param>
        /// <param name="state">Whether to keep only live sessions - <c>all</c> when absent.</param>
        /// <param name="subscriptionId">Subscription to keep, as a uuid, or null.</param>
        /// <param name="ownerId">Owner of the subscription to keep, as a uuid, or null.</param>
        /// <param name="streamId">Opaque stream identifier to keep, as a uuid, or null.</param>
        /// <param name="ip">Address to keep, compared exactly, or null.</param>
        /// <param name="country">Country to keep, any case, or null.</param>
        /// <param name="isp">Provider to keep, compared exactly, or null.</param>
        /// <param name="userAgent">User agent to keep, compared exactly, or null.</param>
        /// <param name="sortBy">Column to order by; <c>startedAt</c> when absent.</param>
        /// <param name="sortOrder">Direction; <c>desc</c> when absent.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with one page, or 400 naming the parameters that were broken.</returns>
        /// <remarks>
        /// <para>
        /// <b>Every parameter is parsed as plain text, case-insensitively.</b> Enum binding takes a
        /// member name spelled in full, so <c>kind=video</c> would be refused while
        /// <c>Kind=VideoStream</c> was accepted - an API that answers a request this conventional
        /// with a 400 teaches its callers to spell things oddly. The mapping lives in
        /// <see cref="ConnectionLogQuery"/>, in Core, so every rule about a value that names
        /// nothing is testable without a running host; this action keeps the HTTP half - the shared
        /// page bounds, the status code and the error body.
        /// </para>
        /// <para>
        /// <b>A <c>kind</c> or <c>state</c> that names nothing is refused, never treated as "no
        /// filter".</b> The screen draws one medium and one state at a time, so a mistyped value
        /// silently widening the list would answer a request nobody made. <c>kind</c> defaults to
        /// <c>video</c> and <c>state</c> to <c>all</c>, because those are the two answers the
        /// untouched screen starts from.
        /// </para>
        /// <para>
        /// <c>total</c> is counted under the same predicate as the page, so a filtered list's pages
        /// sum to its <c>total</c> and every page reports the same one.
        /// </para>
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewSubscriptionsPolicy)]
        [ProducesResponseType<AdminConnectionLogPageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetConnectionLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = ConnectionLogListLimits.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] string? kind = null,
            [FromQuery] string? state = null,
            [FromQuery] string? subscriptionId = null,
            [FromQuery] string? ownerId = null,
            [FromQuery] string? streamId = null,
            [FromQuery] string? ip = null,
            [FromQuery] string? country = null,
            [FromQuery] string? isp = null,
            [FromQuery] string? userAgent = null,
            [FromQuery] string? sortBy = "startedAt",
            [FromQuery] string? sortOrder = "desc",
            CancellationToken cancellationToken = default)
        {
            NoStore();

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            MergeListErrors(
                errors,
                ListQueryErrors.Common(
                    page,
                    pageSize,
                    search,
                    ConnectionLogListLimits.MaxPageSize,
                    ConnectionLogListLimits.MaxSearchLength));

            var query = ConnectionLogTextParser.ParseQuery(
                kind,
                state,
                subscriptionId,
                ownerId,
                streamId,
                ip,
                country,
                isp,
                userAgent,
                sortBy,
                sortOrder,
                out var queryErrors);

            MergeListErrors(errors, queryErrors);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var log = await _logs.GetPageAsync(
                page,
                pageSize,
                search,
                query!.StreamKind,
                query.LiveOnly,
                query.SubscriptionId,
                query.OwnerId,
                query.StreamId,
                query.Ip,
                query.CountryCode,
                query.Isp,
                query.UserAgent,
                query.SortBy,
                query.SortOrder,
                cancellationToken);

            return Ok(new AdminConnectionLogPageResponse
            {
                Items = log.Items.Select(ToResponse).ToList(),
                Total = log.Total,
                Page = log.Page,
                PageSize = log.PageSize
            });
        }

        /// <summary>
        /// Deletes every row of the log - both media, live and finished alike.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>connection_logs_cleared</c>, 400 <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>It ignores the screen's filters on purpose.</b> The button says "clear all logs", so
        /// the whole table goes whatever the reader had narrowed the table to; a delete that obeyed
        /// a filter the caller happened not to send would wipe a different set from the one it
        /// named. It is not paged, and no identifier is passed.
        /// </para>
        /// <para>
        /// <b>An empty table is success, not a refusal.</b> The second press of a button that
        /// already did its job answers 200 again, because the state the caller asked for is the
        /// state that holds. There is no <c>404</c> here: "the log is empty" is what a successful
        /// clear leaves behind, not a missing resource.
        /// </para>
        /// <para>
        /// <b>The antiforgery pair is checked first, before anything else at all.</b> This action
        /// has no query and no body to parse, but it does wipe the installation's evidence; a
        /// request that did not come from this application must be refused as such, and that
        /// refusal has to be the one the caller is told about.
        /// </para>
        /// <para>
        /// It carries <c>editSubscriptions</c> because destroying the log is a change to what the
        /// subscription surface holds, and the read permission alone must not reach it.
        /// </para>
        /// </remarks>
        [HttpDelete]
        [Authorize(Policy = AuthenticationExtensions.EditSubscriptionsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ClearConnectionLogs(CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var removed = await _logs.ClearAllAsync(cancellationToken);

            Logger.LogInformation(
                "Cleared the connection log: {Removed} row(s) deleted.",
                removed);

            return Ok(new MessageResponse
            {
                Code = "connection_logs_cleared",
                Message = removed == 1
                    ? "1 connection log entry was deleted."
                    : $"{removed} connection log entries were deleted."
            });
        }

        private static void MergeListErrors(
            Dictionary<string, string[]> target,
            Dictionary<string, string[]> source)
        {
            foreach (var entry in source)
            {
                target[entry.Key] = entry.Value;
            }
        }

        private static AdminConnectionLogItemResponse ToResponse(
            Fluxy.Core.Subscriptions.ReadModels.ConnectionLogListItem item) => new()
        {
            Id = item.Id.ToString(),
            SubscriptionId = item.SubscriptionId.ToString(),
            OwnerId = item.OwnerId.ToString(),
            Username = item.Username,
            StreamKind = ConnectionLogTextParser.NameOf(item.StreamKind),
            StreamId = item.StreamId.ToString(),
            StreamTitle = item.StreamTitle,
            Ip = item.Ip,
            UserAgent = item.UserAgent,
            CountryCode = item.CountryCode,
            Isp = item.Isp,
            StartedAt = item.StartedAt,
            EndedAt = item.EndedAt,
            BroadcastPid = item.BroadcastPid
        };
    }
}
