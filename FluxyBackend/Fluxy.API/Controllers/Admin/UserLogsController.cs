using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Users;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Users.Interfaces;
using Fluxy.Core.Users.ReadModels;
using Fluxy.Core.Users.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Admin
{
    /// <summary>
    /// The user audit log: every attempt to register, sign in, reset a password, change an
    /// account or sign out, with its outcome and the network it came from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two endpoints, and deliberately only two.</b> The log is read and it is wiped wholesale;
    /// nothing here writes an entry. A row is written by the flow that performed the action it
    /// records, through the single fail-open write path, because an operator editing the audit
    /// trail by hand would make it evidence of nothing.
    /// </para>
    /// <para>
    /// <b>One permission gates both endpoints, which is a deliberate departure.</b> Reading the
    /// log is <c>viewUserLogs</c>; wiping it destroys the same evidence, so it carries the same
    /// grant plus a fresh antiforgery pair rather than inventing a write permission nobody asked
    /// to reason about. There is no <c>editUserLogs</c>: an operator never writes a row, and the
    /// only write this surface performs is the total erase. The owner ratified this explicitly.
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
    /// <c>sortOrder=Descending</c> was accepted. The mapping lives in <see cref="UserLogQuery"/>,
    /// in Core, so every rule about a value that names nothing is testable without a running
    /// host; this action keeps the HTTP half. The repeated filters arrive through
    /// <see cref="HttpRequest.Query"/> because model binding cannot bind a repeated query
    /// parameter to a list while keeping each element's own refusal - one bad element has to
    /// name the parameter it arrived under, not the property it was bound to.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/user-logs")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class UserLogsController : ApiControllerBase
    {
        private readonly IUserLogService _logs;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserLogsController"/> class.
        /// </summary>
        /// <param name="logs">Service that reads and clears the log.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public UserLogsController(
            IUserLogService logs,
            IAntiforgery antiforgery,
            ILogger<UserLogsController> logger)
            : base(antiforgery, logger)
        {
            _logs = logs;
        }

        /// <summary>
        /// Reports one page of the log, filtered and ordered the way the query asked.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many entries one page holds.</param>
        /// <param name="search">Substring of the description or the login name, or null.</param>
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
        /// <b><c>userIds</c>, <c>groupIds</c> and <c>recordTypes</c> are repeated parameters.</b>
        /// A request naming two accounts is <c>?userIds=...&amp;userIds=...</c>; each element is
        /// judged on its own, and one bad element refuses the whole filter under the parameter's
        /// own name. Dropping it would answer a list wider than the one the caller asked for.
        /// </para>
        /// <para>
        /// <b><c>groupIds</c> matches the account's current group, not the group it held when the
        /// event was written.</b> A group is a property of the account, and a row with no account
        /// belongs to no group - so naming a group excludes rows about unknown accounts, which is
        /// the honest answer rather than a silent widening.
        /// </para>
        /// <para>
        /// <b>The network filters are the connection log's, parameter for parameter.</b>
        /// <c>ip</c> and <c>isp</c> keep the stored value exactly, case included; <c>country</c> is
        /// canonicalised to upper-case ISO alpha-2 so a typed <c>de</c> means <c>DE</c>, the same
        /// answer the connection log gives. The three names are what the shared
        /// <c>ConnectionNetworkCell</c> writes, so the same click filters either screen. A null
        /// column never matches, and a blank value is "no filter".
        /// </para>
        /// <para>
        /// <b>Both date bounds are independent and inclusive.</b> A later <c>dateFrom</c> than
        /// <c>dateTo</c> is a valid query describing an empty result: 200 with an empty page and
        /// a total of zero.
        /// </para>
        /// <para>
        /// <c>total</c> is counted under the same predicate as the page, so a filtered list's
        /// pages sum to its <c>total</c> and every page reports the same one.
        /// </para>
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewUserLogsPolicy)]
        [ProducesResponseType<AdminUserLogPageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetUserLogs(
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
                    pageSize ?? UserLogListLimits.DefaultPageSize,
                    search,
                    UserLogListLimits.MaxPageSize,
                    UserLogListLimits.MaxSearchLength));

            var query = UserLogQuery.TryParse(
                page,
                pageSize,
                search,
                QueryValues("userIds"),
                QueryValues("groupIds"),
                QueryValues("recordTypes"),
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

            var log = await _logs.GetPageAsync(
                query!.Page,
                query.PageSize,
                query.Search,
                query.UserIds,
                query.GroupIds,
                query.RecordTypes,
                query.Ip,
                query.CountryCode,
                query.Isp,
                query.DateFrom,
                query.DateTo,
                query.SortBy,
                query.SortOrder,
                cancellationToken);

            return Ok(new AdminUserLogPageResponse
            {
                Items = log.Items.Select(ToResponse).ToList(),
                Total = log.Total,
                Page = log.Page,
                PageSize = log.PageSize
            });
        }

        /// <summary>
        /// Deletes every row of the log - every account, every entry, whatever the screen was
        /// filtered to.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>user_logs_cleared</c>, 400 <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>It ignores the screen's filters on purpose.</b> The button says "clear the user
        /// log", so the whole table goes whatever the reader had narrowed it to; a delete that
        /// obeyed a filter the caller happened not to send would wipe a different set from the
        /// one it named. It is not paged, and no identifier is passed.
        /// </para>
        /// <para>
        /// <b>An empty table is success, not a refusal.</b> The second press of a button that
        /// already did its job answers 200 again, because the state the caller asked for is the
        /// state that holds.
        /// </para>
        /// <para>
        /// <b>The antiforgery pair is checked first, before anything else at all.</b> This action
        /// wipes the installation's audit history; a request that did not come from this
        /// application must be refused as such, and that refusal has to be the one the caller is
        /// told about.
        /// </para>
        /// </remarks>
        [HttpDelete]
        [Authorize(Policy = AuthenticationExtensions.ViewUserLogsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ClearUserLogs(CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var removed = await _logs.ClearAllAsync(cancellationToken);

            Logger.LogInformation(
                "Cleared the user log: {Removed} row(s) deleted.",
                removed);

            return Ok(new MessageResponse
            {
                Code = "user_logs_cleared",
                Message = $"Cleared the user log: {removed} row(s) deleted."
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
        /// Renders one row, recovering the typed identifier when the row names no account.
        /// </summary>
        /// <remarks>
        /// The login name is resolved in the order the contract states: the joined account when
        /// the row still has one, otherwise the identifier the writer typed, read back out of the
        /// stored sentence. The frontend's table draws this field, so the fallback is what makes a
        /// refused sign-in for an unknown name readable at all.
        /// </remarks>
        private static AdminUserLogItemResponse ToResponse(UserLogListItem item)
            => new()
            {
                Id = item.Id.ToString(),
                AccountId = item.UserId?.ToString(),
                Username = item.Username ?? TypedSubject(item.Description),
                RecordType = UserLogRecordTypes.WireName(item.RecordType),
                IsSuccess = item.IsSuccess,
                Description = item.Description,
                Ip = item.Ip,
                Isp = item.Isp,
                CountryCode = item.CountryCode,
                CreatedAt = item.CreatedAt
            };

        /// <summary>The identifier a stored sentence names, or null when it names none.</summary>
        private static string? TypedSubject(string description)
            => UserLogDescriptions.TryGetSubject(description, out var subject) ? subject : null;
    }
}
