using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Interfaces;
using Fluxy.Core.Subscriptions.ReadModels;
using Fluxy.Core.Subscriptions.Repositories;

namespace Fluxy.Application.Subscriptions.Services
{
    /// <summary>
    /// Reads the streaming connection log and wipes it on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A thin service, and deliberately so: every rule this feature has is either a bound the
    /// transport refuses before the call or a predicate the repository translates into SQL. What
    /// is left here is the one decision that is neither - that clearing the log is a whole-table
    /// operation rather than a filtered one - and the delegation that keeps the API naming a Core
    /// contract instead of a repository.
    /// </para>
    /// <para>
    /// <b>Nothing here writes a log row.</b> The process that relays a stream will own the capture
    /// path, and it arrives in its own pass with its own rules - which allowance was spent, which
    /// moment the row opens. This service reads and clears, and that is the whole of its surface.
    /// </para>
    /// <para>
    /// <b>GeoIP is never consulted.</b> Country and provider are stored with the row for the reason
    /// the entity's remarks give: a value resolved while a page is built cannot be filtered or
    /// counted on the server.
    /// </para>
    /// </remarks>
    public sealed class ConnectionLogService : IConnectionLogService
    {
        private readonly IConnectionLogRepository _logs;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionLogService"/> class.
        /// </summary>
        /// <param name="logs">Repository holding the connection log.</param>
        public ConnectionLogService(IConnectionLogRepository logs)
        {
            _logs = logs;
        }

        /// <inheritdoc />
        public Task<ConnectionLogPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            ConnectionStreamKind streamKind,
            bool liveOnly,
            Guid? subscriptionId,
            Guid? ownerId,
            Guid? streamId,
            string? ip,
            string? countryCode,
            string? isp,
            string? userAgent,
            ConnectionLogSortField sortBy,
            ConnectionLogSortOrder sortOrder,
            CancellationToken cancellationToken = default)
            => _logs.GetPageAsync(
                page,
                pageSize,
                search,
                streamKind,
                liveOnly,
                subscriptionId,
                ownerId,
                streamId,
                ip,
                countryCode,
                isp,
                userAgent,
                sortBy,
                sortOrder,
                cancellationToken);

        /// <inheritdoc />
        public Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
            // No filter is passed because there is none to pass: the button says "clear all
            // logs", and a delete that obeyed the screen's filters would remove a different set
            // from the one the caller could see it naming. Live and finished rows and both media
            // go together, and an already-empty table answers zero rather than a refusal - the
            // second press of a button that already did its job is not an error.
            => _logs.ClearAllAsync(cancellationToken);
    }
}
