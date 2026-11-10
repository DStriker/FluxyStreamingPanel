using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Streams;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Streams.Interfaces;
using Fluxy.Core.Streams.ReadModels;
using Fluxy.Core.Streams.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Admin
{
    /// <summary>
    /// The journal of problems the stream producer reports: which stream failed, what it said, and
    /// when.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two endpoints, and deliberately only two - this is the write fence.</b> The journal is
    /// read and it is wiped wholesale; nothing here records a problem. A row is written by the
    /// future stream producer through <c>IStreamProblemService.RecordAsync</c> and, in Development,
    /// by the trial seeder. An operator typing a problem by hand would make the journal evidence of
    /// nothing, so a <c>POST</c>, a <c>PUT</c>, a <c>PATCH</c> or a <c>DELETE {id}</c> declared now
    /// would be a route with no rule behind it. The fence is the absence of those routes rather
    /// than a check inside one, and the collection answers <c>405 method_not_allowed</c> to every
    /// method it does not own.
    /// </para>
    /// <para>
    /// <b>The read and the clear sit on the parent resource's grant pair, which is a deliberate
    /// departure.</b> The other logs gate both verbs with one grant, because there is no write
    /// permission for an audit trail. Here the parent resource already has two - reading a stream
    /// and changing it - and this journal is a read of the streams' history and a change to what
    /// the streams surface holds. A group that may watch the streams may read their problems; only
    /// a group that may change the streams may wipe the journal. Reusing <c>viewStreams</c> for
    /// both would let a read-only grant destroy rows, and inventing a third permission would split
    /// one screen's access into three grants nobody asked to reason about.
    /// </para>
    /// <para>
    /// <b>The clear is a <c>DELETE</c> on the collection, and that is the second departure.</b>
    /// It removes every entry rather than the ones the screen happened to be filtered to, so it
    /// names no member of the collection - the button says "clear the journal", and a delete that
    /// obeyed a filter the caller did not send would wipe a different set from the one it named.
    /// Both departures are stated in full here, where a reader meets the route; the
    /// repository-wide list of departures gains them with the feature's documentation pass.
    /// </para>
    /// <para>
    /// <b>Every parameter is parsed as plain text, case-insensitively.</b> Enum binding takes a
    /// member name spelled in full, so <c>sortOrder=desc</c> would be refused while
    /// <c>sortOrder=Descending</c> was accepted. The mapping lives in
    /// <see cref="StreamProblemQuery"/>, in Core, so every rule about a value that names nothing is
    /// testable without a running host; this action keeps the HTTP half - the status code and the
    /// body.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/stream-problems")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class StreamProblemsController : ApiControllerBase
    {
        private readonly IStreamProblemService _problems;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamProblemsController"/> class.
        /// </summary>
        /// <param name="problems">Service that reads and clears the journal.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public StreamProblemsController(
            IStreamProblemService problems,
            IAntiforgery antiforgery,
            ILogger<StreamProblemsController> logger)
            : base(antiforgery, logger)
        {
            _problems = problems;
        }

        /// <summary>
        /// Reports one page of the journal, filtered and ordered the way the query asked.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many entries one page holds.</param>
        /// <param name="search">Case-insensitive substring of the message, or null.</param>
        /// <param name="streamId">Only problems of this stream, as text, or null.</param>
        /// <param name="dateFrom">Earliest creation instant to keep, inclusive, or null.</param>
        /// <param name="dateTo">Latest creation instant to keep, inclusive, or null.</param>
        /// <param name="sortBy">Column to order by; <c>createdAt</c> when absent.</param>
        /// <param name="sortOrder">Direction; <c>desc</c> when absent.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with one page, or 400 naming the parameters that were broken.</returns>
        /// <remarks>
        /// <para>
        /// <b>Nothing is clamped.</b> The page bounds are the caller's to break and this endpoint's
        /// to refuse; a request for 5000 rows is a <c>400</c> naming <c>pageSize</c>, never a page
        /// of 1000 the caller was not told about.
        /// </para>
        /// <para>
        /// <b>Both date bounds are independent and inclusive.</b> A later <c>dateFrom</c> than
        /// <c>dateTo</c> is a valid query describing an empty result: 200 with an empty page and a
        /// total of zero. <c>total</c> is counted under the same predicate as the page, so a
        /// filtered list's pages sum to its <c>total</c> and every page reports the same one.
        /// </para>
        /// <para>
        /// <b>The order is <c>createdAt</c> descending with an <c>id</c> tie-break, and it is the
        /// repository's.</b> Two problems can be reported in the same instant; without the
        /// identifier the database is free to answer a different order on every read, which would
        /// make a page turn show one row twice and skip another.
        /// </para>
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewStreamsPolicy)]
        [ProducesResponseType<AdminStreamProblemPageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetStreamProblems(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null,
            [FromQuery] string? search = null,
            [FromQuery] string? streamId = null,
            [FromQuery] string? dateFrom = null,
            [FromQuery] string? dateTo = null,
            [FromQuery] string? sortBy = "createdAt",
            [FromQuery] string? sortOrder = "desc",
            CancellationToken cancellationToken = default)
        {
            NoStore();

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            MergeListErrors(
                errors,
                ListQueryErrors.Common(
                    page ?? 1,
                    pageSize ?? StreamProblemListLimits.DefaultPageSize,
                    search,
                    StreamProblemListLimits.MaxPageSize,
                    StreamProblemListLimits.MaxSearchLength));

            var query = StreamProblemQuery.TryParse(
                page,
                pageSize,
                search,
                streamId,
                dateFrom,
                dateTo,
                sortBy,
                sortOrder,
                out var queryErrors);

            MergeListErrors(errors, queryErrors);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var journal = await _problems.GetPageAsync(
                query!.Page,
                query.PageSize,
                query.Search,
                query.StreamId,
                query.DateFrom,
                query.DateTo,
                query.SortBy,
                query.SortOrder,
                cancellationToken);

            return Ok(new AdminStreamProblemPageResponse
            {
                Items = journal.Items.Select(ToResponse).ToList(),
                Total = journal.Total,
                Page = journal.Page,
                PageSize = journal.PageSize
            });
        }

        /// <summary>
        /// Deletes every row of the journal - every stream, every message, whatever the screen was
        /// filtered to.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>stream_problems_cleared</c>, 400 <c>csrf_invalid</c> without the antiforgery
        /// pair.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>It ignores the screen's filters on purpose.</b> The button says "clear the journal",
        /// so the whole table goes whatever the reader had narrowed it to; a delete that obeyed a
        /// filter the caller happened not to send would wipe a different set from the one it named.
        /// It is not paged, and no identifier is passed.
        /// </para>
        /// <para>
        /// <b>An empty table is success, not a refusal.</b> The second press of a button that
        /// already did its job answers 200 again with a count of zero, because the state the caller
        /// asked for is the state that holds.
        /// </para>
        /// <para>
        /// <b>The antiforgery pair is checked first, before anything else at all.</b> This action
        /// wipes the installation's record of stream failures; a request that did not come from
        /// this application must be refused as such, and that refusal has to be the one the caller
        /// is told about.
        /// </para>
        /// </remarks>
        [HttpDelete]
        [Authorize(Policy = AuthenticationExtensions.EditStreamsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ClearStreamProblems(CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var removed = await _problems.ClearAllAsync(cancellationToken);

            Logger.LogInformation(
                "Cleared the stream problem journal: {Removed} row(s) deleted.",
                removed);

            return Ok(new MessageResponse
            {
                Code = StreamProblemResponses.ClearedCode,
                Message = $"Cleared the stream problem journal: {removed} row(s) deleted."
            });
        }

        /// <summary>
        /// Adds every entry of a refusal map to the one this action answers with.
        /// </summary>
        private static void MergeListErrors(
            Dictionary<string, string[]> target,
            Dictionary<string, string[]> source)
        {
            foreach (var entry in source)
            {
                target[entry.Key] = entry.Value;
            }
        }

        /// <summary>
        /// Renders one row: the stored message and the stream's name as it reads now.
        /// </summary>
        /// <remarks>
        /// The name comes from the join in the read path, never from a snapshot on the row - the
        /// schema deliberately stores no name, so a renamed stream draws its current name on every
        /// page. A row whose name the read path did not resolve answers an empty cell rather than
        /// failing the page.
        /// </remarks>
        private static AdminStreamProblemItemResponse ToResponse(StreamProblemListItem item)
            => new()
            {
                Id = item.Id.ToString(),
                StreamId = item.StreamId.ToString(),
                StreamName = item.StreamName ?? string.Empty,
                ErrorMessage = item.ErrorMessage,
                CreatedAt = item.CreatedAt
            };
    }
}
