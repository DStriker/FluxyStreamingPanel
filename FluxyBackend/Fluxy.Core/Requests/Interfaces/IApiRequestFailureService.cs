using Fluxy.Core.Requests.Enums;
using Fluxy.Core.Requests.ReadModels;

namespace Fluxy.Core.Requests.Interfaces
{
    /// <summary>
    /// Reads the API request failure log and wipes it on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A service of its own rather than extra methods on a subscription or blacklist service: a
    /// refusal names facts from several domains, and the record of refusals is not the rules that
    /// caused them.
    /// </para>
    /// <para>
    /// <b>There is deliberately no write method here.</b> This capability contains no production
    /// write: the future public API will record refusals through its own path, and the only insert
    /// this pass has is the Development seeder, which reaches the repository directly. A
    /// <c>Record</c>/<c>Save</c> method on this interface would be an invitation to build the
    /// endpoint the fence exists to prevent.
    /// </para>
    /// <para>
    /// The interface says nothing about HTTP, codes or cookies: which outcome becomes a 200 is the
    /// transport layer's business.
    /// </para>
    /// </remarks>
    public interface IApiRequestFailureService
    {
        /// <summary>Reads one filtered page of the log, counted before paging.</summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many records one page holds.</param>
        /// <param name="search">
        /// Case-insensitive substring of the description, the subscription data or the content
        /// name, or null.
        /// </param>
        /// <param name="reasons">Reasons to keep, or null for every reason.</param>
        /// <param name="ip">Address to keep, compared exactly, or null.</param>
        /// <param name="countryCode">Upper-case ISO alpha-2 country to keep, or null.</param>
        /// <param name="isp">Provider to keep, compared exactly, or null.</param>
        /// <param name="dateFrom">Earliest <c>created_at</c> to keep, inclusive, or null.</param>
        /// <param name="dateTo">Latest <c>created_at</c> to keep, inclusive, or null.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<ApiRequestFailurePage> GetPageAsync(
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
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes every row of the log, whatever the reader had filtered the screen to.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>How many records were removed; zero when the table was already empty.</returns>
        Task<int> ClearAllAsync(CancellationToken cancellationToken = default);
    }
}
