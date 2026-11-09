using Fluxy.Application.Authentication.Utils;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Users.Commands;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Interfaces;
using Fluxy.Core.Users.ReadModels;
using Fluxy.Core.Users.Repositories;
using Fluxy.Core.Users.Utils;
using Microsoft.Extensions.Logging;

namespace Fluxy.Application.Users.Services
{
    /// <summary>
    /// Writes the user audit log, reads it a page at a time, and clears it on an administrator's
    /// behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One write path, and it fails open.</b> A log entry describes something that has already
    /// happened - a registration, a sign-in, a password change - so a failure to store the
    /// description must not turn a successful operation into an error. <see cref="RecordAsync"/>
    /// resolves the snapshot, stages the row, commits it with its own unit of work, and turns a
    /// failure into a warning. That is the same fail-open answer the GeoIP lookup, Redis and the
    /// mailer already give: an observability feature is not a precondition for the flow it observes.
    /// </para>
    /// <para>
    /// <b>The snapshot is taken here, not by the caller.</b> The service asks
    /// <see cref="GeoIpMemo"/> for the request's address - a memo, so a request that records two
    /// events does one lookup - and stores the country upper-cased and the organization as the
    /// provider. The Core <c>IGeoIpResolver</c> has no <c>Isp</c> field: its
    /// <c>Organization</c> is what the connection log calls the provider.
    /// </para>
    /// <para>
    /// <b>Column widths are enforced at the write path.</b> An address, provider or user agent
    /// longer than its column is shortened rather than refused, for the reason the token service
    /// gives about the same three facts: these columns record where a request came from and nothing
    /// depends on them being complete.
    /// </para>
    /// </remarks>
    public sealed class UserLogService : IUserLogService
    {
        private readonly IUserLogRepository _logs;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GeoIpMemo _geoIp;
        private readonly ILogger<UserLogService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserLogService"/> class.
        /// </summary>
        /// <param name="logs">Repository that stages the row and answers the reads.</param>
        /// <param name="unitOfWork">Unit of work the record commit belongs to.</param>
        /// <param name="geoIp">The lookups of this request, memoized per address.</param>
        /// <param name="logger">Logger a swallowed write failure is reported to.</param>
        public UserLogService(
            IUserLogRepository logs,
            IUnitOfWork unitOfWork,
            GeoIpMemo geoIp,
            ILogger<UserLogService> logger)
        {
            _logs = logs;
            _unitOfWork = unitOfWork;
            _geoIp = geoIp;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task RecordAsync(
            NewUserLog entry,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var geo = await _geoIp.ResolveAsync(entry.Ip, cancellationToken);

                // The snapshot is a fact about the request, so it is taken once and stored as it
                // was. The country is upper-cased to the form the column and a case-insensitive
                // filter both expect; the resolver's organization is the provider.
                var resolved = entry with
                {
                    Ip = Truncate(entry.Ip, UserLogListLimits.IpMaxLength),
                    Isp = Truncate(geo.Organization, UserLogListLimits.IspMaxLength),
                    CountryCode = Truncate(
                        geo.CountryCode?.ToUpperInvariant(),
                        UserLogListLimits.CountryCodeLength),
                    UserAgent = Truncate(entry.UserAgent, UserLogListLimits.UserAgentMaxLength),
                    Description = TruncateText(
                        entry.Description,
                        UserLogDescriptions.DescriptionMaxLength)
                };

                await _logs.AddAsync(resolved, cancellationToken);

                // Its own save, not the caller's: the flow that records an event has already
                // committed its business change, and holding that transaction open for an optional
                // row would let the audit trail dictate the cost of the operation it describes.
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller went away, so there is no HTTP result left to protect. Not a log
                // failure worth reporting.
                throw;
            }
            catch (Exception exception)
            {
                // An audit failure must never change the result of the operation being audited.
                // The entry is lost, the flow continues, and the reason is kept in the log for the
                // operator rather than handed to the client.
                //
                // The staged row is dropped first: a failed save leaves it in the request's change
                // tracker, and the next save of the same request would retry the insert - which is
                // the "call RecordAsync last" rule this method exists to make unnecessary.
                _logs.DiscardStaged();

                _logger.LogWarning(
                    exception,
                    "Could not record the user log entry of type {RecordType} for account " +
                    "{UserId}. The operation it describes is unaffected.",
                    entry.RecordType,
                    entry.UserId);
            }
        }

        /// <inheritdoc />
        public Task<UserLogPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            IReadOnlyList<Guid>? userIds,
            IReadOnlyList<Guid>? groupIds,
            IReadOnlyList<UserLogRecordType>? recordTypes,
            string? ip,
            string? countryCode,
            string? isp,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            UserLogSortField sortBy,
            UserLogSortOrder sortOrder,
            CancellationToken cancellationToken = default)
            => _logs.GetPageAsync(
                page,
                pageSize,
                search,
                userIds,
                groupIds,
                recordTypes,
                ip,
                countryCode,
                isp,
                dateFrom,
                dateTo,
                sortBy,
                sortOrder,
                cancellationToken);

        /// <inheritdoc />
        public Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
            // No filter is passed because there is none to pass: the button says "clear all logs",
            // and a delete that obeyed the screen's filters would remove a different set from the
            // one the caller could see it naming. An already-empty table answers zero rather than a
            // refusal - the second press of a button that already did its job is not an error.
            => _logs.ClearAllAsync(cancellationToken);

        /// <summary>
        /// Cuts a value to what the column holds.
        /// </summary>
        /// <remarks>
        /// The same rule the token service applies to the address and the agent: a value that does
        /// not fit is shortened, because refusing to describe an event over the length of a user
        /// agent would lose the whole entry.
        /// </remarks>
        private static string? Truncate(string? value, int maxLength)
            => value is null || value.Length <= maxLength
                ? value
                : value[..maxLength];

        /// <summary>Cuts a value the column requires to what it holds, never returning null.</summary>
        private static string TruncateText(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Length <= maxLength
                ? value
                : value[..maxLength];
        }
    }
}
