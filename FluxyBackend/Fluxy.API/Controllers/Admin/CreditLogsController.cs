using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Users;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Users.Interfaces;
using Fluxy.Core.Users.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Admin
{
    /// <summary>
    /// The reseller credit log: the audit trail of how a reseller's balance got to where it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two endpoints, and deliberately only two.</b> The log is read and it is wiped wholesale;
    /// nothing here issues or spends credit. Those writes belong to the account create and update
    /// flows, which stage the balance change and its log row in one save - the reason a
    /// <c>POST</c> declared here would be a second way to change a balance behind the account
    /// service's back.
    /// </para>
    /// <para>
    /// <b>No new permission.</b> Reading a reseller's balance history is reading the reseller
    /// surface, so it carries <c>viewResellers</c>; wiping it destroys the evidence that surface is
    /// about, so it carries <c>editResellers</c> plus a fresh antiforgery pair. The owner ratified
    /// the reuse explicitly, and no catalog, migration or frontend mirror changes with it.
    /// </para>
    /// <para>
    /// <b>Only two columns are sortable, and both are columns of <c>credit_logs</c>.</b> The
    /// reseller's username is joined for the page, so an order built on it would order a page the
    /// server had already cut. Sorting is limited to <c>createdAt</c> and <c>amount</c> for that
    /// reason.
    /// </para>
    /// <para>
    /// <b>The page size ceiling is 1000, refused rather than clamped.</b> Silently turning a
    /// request for 5000 rows into 1000 would answer with a page whose items do not match the size
    /// the caller was told it got.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/credit-logs")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class CreditLogsController : ApiControllerBase
    {
        private readonly ICreditLogService _logs;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreditLogsController"/> class.
        /// </summary>
        /// <param name="logs">Service that reads and clears the log.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public CreditLogsController(
            ICreditLogService logs,
            IAntiforgery antiforgery,
            ILogger<CreditLogsController> logger)
            : base(antiforgery, logger)
        {
            _logs = logs;
        }

        /// <summary>
        /// Reports one page of the log, filtered and ordered the way the query asked.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many entries one page holds.</param>
        /// <param name="search">Substring of the description or the reseller's username, or null.</param>
        /// <param name="resellerId">Reseller to keep, as a uuid, or null.</param>
        /// <param name="dateFrom">Earliest creation instant to keep, inclusive, or null.</param>
        /// <param name="dateTo">Latest creation instant to keep, inclusive, or null.</param>
        /// <param name="sortBy">Column to order by; <c>createdAt</c> when absent.</param>
        /// <param name="sortOrder">Direction; <c>desc</c> when absent.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with one page, or 400 naming the parameters that were broken.</returns>
        /// <remarks>
        /// <para>
        /// <b>Every parameter is parsed as plain text, case-insensitively.</b> Enum binding takes a
        /// member name spelled in full, so <c>sortOrder=desc</c> would be refused while
        /// <c>sortOrder=Descending</c> was accepted. The mapping lives in
        /// <see cref="CreditLogQuery"/>, in Core, so every rule about a value that names nothing is
        /// testable without a running host; this action keeps the HTTP half - the shared page
        /// bounds, the status code and the error body.
        /// </para>
        /// <para>
        /// <b>The two date bounds are independent and inclusive.</b> <c>dateFrom</c> keeps rows
        /// written at or after the instant, <c>dateTo</c> keeps rows written at or before it, and
        /// either may be absent. Both are inclusive because the picker names whole days. A blank
        /// value is "no bound" and an unparseable one is refused under its own parameter - never
        /// read as "no bound", which would widen the list silently.
        /// </para>
        /// <para>
        /// <c>total</c> is counted under the same predicate as the page, so a filtered list's pages
        /// sum to its <c>total</c> and every page reports the same one.
        /// </para>
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewResellersPolicy)]
        [ProducesResponseType<AdminCreditLogPageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetCreditLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = CreditLogListLimits.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] string? resellerId = null,
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
                    page,
                    pageSize,
                    search,
                    CreditLogListLimits.MaxPageSize,
                    CreditLogListLimits.MaxSearchLength));

            var query = CreditLogQuery.TryParse(
                resellerId,
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

            var log = await _logs.GetPageAsync(
                page,
                pageSize,
                search,
                query!.ResellerId,
                query.DateFrom,
                query.DateTo,
                query.SortBy,
                query.SortOrder,
                cancellationToken);

            return Ok(new AdminCreditLogPageResponse
            {
                Items = log.Items.Select(ToResponse).ToList(),
                Total = log.Total,
                Page = log.Page,
                PageSize = log.PageSize
            });
        }

        /// <summary>
        /// Deletes every row of the log - every reseller, every entry, whatever the screen was
        /// filtered to.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>credit_logs_cleared</c>, 400 <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>It ignores the screen's filters on purpose.</b> The button says "clear the credit
        /// log", so the whole table goes whatever the reader had narrowed it to; a delete that
        /// obeyed a filter the caller happened not to send would wipe a different set from the one
        /// it named. It is not paged, and no identifier is passed.
        /// </para>
        /// <para>
        /// <b>An empty table is success, not a refusal.</b> The second press of a button that
        /// already did its job answers 200 again, because the state the caller asked for is the
        /// state that holds. There is no <c>404</c> here: "the log is empty" is what a successful
        /// clear leaves behind, not a missing resource.
        /// </para>
        /// <para>
        /// <b>The antiforgery pair is checked first, before anything else at all.</b> This action
        /// has no query and no body to parse, but it does wipe the installation's credit history; a
        /// request that did not come from this application must be refused as such, and that
        /// refusal has to be the one the caller is told about.
        /// </para>
        /// </remarks>
        [HttpDelete]
        [Authorize(Policy = AuthenticationExtensions.EditResellersPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ClearCreditLogs(CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var removed = await _logs.ClearAllAsync(cancellationToken);

            Logger.LogInformation(
                "Cleared the credit log: {Removed} row(s) deleted.",
                removed);

            return Ok(new MessageResponse
            {
                Code = "credit_logs_cleared",
                Message = $"Cleared the credit log: {removed} row(s) deleted."
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

        private static AdminCreditLogItemResponse ToResponse(
            Fluxy.Core.Users.ReadModels.CreditLogListItem item) => new()
        {
            Id = item.Id.ToString(),
            ResellerId = item.ResellerId.ToString(),
            ResellerUsername = item.ResellerUsername,
            IssuedById = item.IssuedById?.ToString(),
            IssuedByUsername = item.IssuedByUsername,
            Amount = item.Amount,
            Description = item.Description,
            CreatedAt = item.CreatedAt
        };
    }
}
