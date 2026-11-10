using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Requests;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Requests.Interfaces;
using Fluxy.Core.Requests.ReadModels;
using Fluxy.Core.Requests.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Admin
{
    /// <summary>
    /// The log of refused public-API requests: which request was refused, why, for which
    /// subscription data and which content, and from where.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two endpoints, and deliberately only two - this is the write-path fence.</b> The log is
    /// read and it is wiped wholesale; nothing here records a refusal. A row is written by the
    /// public API's capture path, which is not part of this capability, and an operator editing the
    /// record of refusals by hand would make it evidence of nothing. A <c>POST</c>, a <c>PATCH</c>
    /// or a <c>DELETE {id}</c> declared now would be a route with no rule behind it, so the fence is
    /// the absence of those routes rather than a check inside one.
    /// </para>
    /// <para>
    /// <b>One permission gates both endpoints, which is a deliberate departure.</b> Reading the
    /// log is <c>viewApiRequestFailures</c>; wiping it destroys the same evidence, so it carries the
    /// same grant plus a fresh antiforgery pair rather than inventing a write permission nobody
    /// asked to reason about. There is no <c>editApiRequestFailures</c>: an operator never writes a
    /// row, and the only write this surface performs is the total erase. This is the owner-ratified
    /// user-logs pattern, restated for this log because reusing it is a choice.
    /// </para>
    /// <para>
    /// <b>The clear is a <c>DELETE</c> on the collection, and that is the second departure.</b>
    /// It removes every entry rather than the ones the screen happened to be filtered to, so it
    /// names no member of the collection - the button says "clear the log", and a delete that
    /// obeyed a filter the caller did not send would wipe a different set from the one it named.
    /// Both departures are recorded in AGENTS.md.
    /// </para>
    /// <para>
    /// <b>Every parameter is parsed as plain text, case-insensitively.</b> Enum binding takes a
    /// member name spelled in full, so <c>sortOrder=desc</c> would be refused while
    /// <c>sortOrder=Descending</c> was accepted. The mapping lives in
    /// <see cref="ApiRequestFailureQuery"/>, in Core, so every rule about a value that names
    /// nothing is testable without a running host; this action keeps the HTTP half. The repeated
    /// <c>reasons</c> filter arrives through <see cref="HttpRequest.Query"/> because model binding
    /// cannot bind a repeated query parameter to a list while keeping each element's own refusal -
    /// one bad element has to name the parameter it arrived under.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/api-request-failures")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class ApiRequestFailuresController : ApiControllerBase
    {
        private readonly IApiRequestFailureService _failures;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiRequestFailuresController"/> class.
        /// </summary>
        /// <param name="failures">Service that reads and clears the log.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public ApiRequestFailuresController(
            IApiRequestFailureService failures,
            IAntiforgery antiforgery,
            ILogger<ApiRequestFailuresController> logger)
            : base(antiforgery, logger)
        {
            _failures = failures;
        }

        /// <summary>
        /// Reports one page of the log, filtered and ordered the way the query asked.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many entries one page holds.</param>
        /// <param name="search">
        /// Substring of the description, the subscription data or the content name, or null.
        /// </param>
        /// <param name="ip">Address to keep, compared exactly, or null.</param>
        /// <param name="country">Country to keep, any case, or null.</param>
        /// <param name="isp">Provider to keep, compared exactly, or null.</param>
        /// <param name="dateFrom">Earliest creation instant to keep, inclusive, or null.</param>
        /// <param name="dateTo">Latest creation instant to keep, inclusive, or null.</param>
        /// <param name="sortBy">Column to order by; <c>createdAt</c> when absent.</param>
        /// <param name="sortOrder">Direction; <c>desc</c> when absent.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with one page, or 400 naming the parameters that were broken.</returns>
        /// <remarks>
        /// <para>
        /// <b><c>reasons</c> is a repeated parameter.</b> A request naming two reasons is
        /// <c>?reasons=auth_failed&amp;reasons=user_blocked</c>; each element is judged on its own,
        /// and one bad element refuses the whole filter under the parameter's own name. Dropping it
        /// would answer a list wider than the one the caller asked for. Duplicates are
        /// deduplicated in the order sent.
        /// </para>
        /// <para>
        /// <b>The network filters are the connection log's, parameter for parameter.</b>
        /// <c>ip</c> and <c>isp</c> keep the stored value exactly, case included; <c>country</c> is
        /// canonicalised to upper-case ISO alpha-2. The three names are what the shared
        /// <c>ConnectionNetworkCell</c> writes, so the same click filters either screen. A null
        /// column never matches, and a blank value is "no filter".
        /// </para>
        /// <para>
        /// <b>Both date bounds are independent and inclusive.</b> A later <c>dateFrom</c> than
        /// <c>dateTo</c> is a valid query describing an empty result: 200 with an empty page and
        /// a total of zero. <c>total</c> is counted under the same predicate as the page, so a
        /// filtered list's pages sum to its <c>total</c> and every page reports the same one.
        /// </para>
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewApiRequestFailuresPolicy)]
        [ProducesResponseType<AdminApiRequestFailurePageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetApiRequestFailures(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null,
            [FromQuery] string? search = null,
            [FromQuery] string? ip = null,
            [FromQuery] string? country = null,
            [FromQuery] string? isp = null,
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
                    pageSize ?? ApiRequestFailureListLimits.DefaultPageSize,
                    search,
                    ApiRequestFailureListLimits.MaxPageSize,
                    ApiRequestFailureListLimits.MaxSearchLength));

            var query = ApiRequestFailureQuery.TryParse(
                page,
                pageSize,
                search,
                QueryValues("reasons"),
                ip,
                country,
                isp,
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

            var log = await _failures.GetPageAsync(
                query!.Page,
                query.PageSize,
                query.Search,
                query.Reasons,
                query.Ip,
                query.CountryCode,
                query.Isp,
                query.DateFrom,
                query.DateTo,
                query.SortBy,
                query.SortOrder,
                cancellationToken);

            return Ok(new AdminApiRequestFailurePageResponse
            {
                Items = log.Items.Select(ToResponse).ToList(),
                Total = log.Total,
                Page = log.Page,
                PageSize = log.PageSize
            });
        }

        /// <summary>
        /// Deletes every row of the log - every reason, every network, whatever the screen was
        /// filtered to.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>api_request_failures_cleared</c>, 400 <c>csrf_invalid</c> without the antiforgery
        /// pair.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>It ignores the screen's filters on purpose.</b> The button says "clear the log", so
        /// the whole table goes whatever the reader had narrowed it to; a delete that obeyed a
        /// filter the caller happened not to send would wipe a different set from the one it named.
        /// It is not paged, and no identifier is passed.
        /// </para>
        /// <para>
        /// <b>An empty table is success, not a refusal.</b> The second press of a button that
        /// already did its job answers 200 again, because the state the caller asked for is the
        /// state that holds.
        /// </para>
        /// <para>
        /// <b>The antiforgery pair is checked first, before anything else at all.</b> This action
        /// wipes the installation's record of refusals; a request that did not come from this
        /// application must be refused as such, and that refusal has to be the one the caller is
        /// told about.
        /// </para>
        /// </remarks>
        [HttpDelete]
        [Authorize(Policy = AuthenticationExtensions.ViewApiRequestFailuresPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ClearApiRequestFailures(CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var removed = await _failures.ClearAllAsync(cancellationToken);

            Logger.LogInformation(
                "Cleared the API request failure log: {Removed} row(s) deleted.",
                removed);

            return Ok(new MessageResponse
            {
                Code = "api_request_failures_cleared",
                Message = $"Cleared the API request failure log: {removed} row(s) deleted."
            });
        }

        /// <summary>
        /// Reads one repeated query parameter as the list of values the caller sent.
        /// </summary>
        /// <returns>The values sent, or null when the parameter was absent.</returns>
        private IReadOnlyList<string>? QueryValues(string name)
        {
            var values = Request.Query[name];

            return values.Count == 0
                ? null
                : values.Where(value => value is not null).Select(value => value!).ToArray();
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
        /// Renders one row: the content snapshot as stored, the reason as its wire name.
        /// </summary>
        /// <remarks>
        /// The kind is the <c>ContentType</c> member name (<c>"VideoStream"</c> ...), which is the
        /// spelling the content-categories contract already speaks; the reason is the snake_case
        /// wire name the filter accepts. Neither is resolved from another table - the row is a
        /// snapshot, so the answer is what it stored.
        /// </remarks>
        private static AdminApiRequestFailureItemResponse ToResponse(ApiRequestFailureListItem item)
            => new()
            {
                Id = item.Id.ToString(),
                SubscriptionData = item.SubscriptionData,
                ContentKind = item.ContentKind?.ToString(),
                ContentId = item.ContentId?.ToString(),
                ContentName = item.ContentName,
                Reason = ApiRequestFailureReasons.WireName(item.Reason),
                Ip = item.Ip,
                Isp = item.Isp,
                CountryCode = item.CountryCode,
                Description = item.Description,
                CreatedAt = item.CreatedAt
            };
    }
}
