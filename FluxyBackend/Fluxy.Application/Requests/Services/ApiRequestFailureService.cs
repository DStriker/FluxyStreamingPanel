using Fluxy.Core.Requests.Enums;
using Fluxy.Core.Requests.Interfaces;
using Fluxy.Core.Requests.ReadModels;
using Fluxy.Core.Requests.Repositories;

namespace Fluxy.Application.Requests.Services
{
    /// <summary>
    /// Reads the API request failure log and wipes it on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A thin service, and deliberately so: every rule this feature has is either a bound the
    /// transport refuses before the call or a predicate the repository translates into SQL. What is
    /// left here is the one decision that is neither - that clearing the log is a whole-table
    /// operation rather than a filtered one - and the delegation that keeps the API naming a Core
    /// contract instead of a repository.
    /// </para>
    /// <para>
    /// <b>Nothing here writes a failure row.</b> The future public API will own the capture path,
    /// and it arrives in its own pass with its own rules - why a request was refused, which
    /// subscription data it named. This service reads and clears, and that is the whole of its
    /// surface. The Development seeder is the only writer and it does not go through this type.
    /// </para>
    /// </remarks>
    public sealed class ApiRequestFailureService : IApiRequestFailureService
    {
        private readonly IApiRequestFailureRepository _failures;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiRequestFailureService"/> class.
        /// </summary>
        /// <param name="failures">Repository holding the failure log.</param>
        public ApiRequestFailureService(IApiRequestFailureRepository failures)
        {
            _failures = failures;
        }

        /// <inheritdoc />
        public Task<ApiRequestFailurePage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            IReadOnlyList<ApiRequestFailureReason>? reasons,
            string? ip,
            string? countryCode,
            string? isp,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            ApiRequestFailureSortField sortBy,
            ApiRequestFailureSortOrder sortOrder,
            CancellationToken cancellationToken = default)
            => _failures.GetPageAsync(
                page,
                pageSize,
                search,
                reasons,
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
            // No filter is passed because there is none to pass: the button says "clear the log",
            // and a delete that obeyed the screen's filters would remove a different set from the
            // one the caller could see it naming. An already-empty table answers zero rather than a
            // refusal - the second press of a button that already did its job is not an error.
            => _failures.ClearAllAsync(cancellationToken);
    }
}
