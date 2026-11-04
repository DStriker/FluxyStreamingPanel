using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.ReadModels;

namespace Fluxy.Core.Subscriptions.Interfaces
{
    /// <summary>
    /// Reads the streaming connection log and wipes it on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A service of its own rather than extra methods on <c>ISubscriptionService</c>: a
    /// subscription is the thing an operator issues, and this is the record of what was watched
    /// under it. The log is read-only and never written here, so its only rules are the bounds the
    /// request states - which the transport has already parsed - and the clear-all it answers.
    /// </para>
    /// <para>
    /// The interface says nothing about HTTP, codes or cookies: which outcome becomes a 200 is the
    /// transport layer's business.
    /// </para>
    /// </remarks>
    public interface IConnectionLogService
    {
        /// <summary>Reads one filtered page of the log, counted before paging.</summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many connections one page holds.</param>
        /// <param name="search">Case-insensitive substring of the address, user agent or provider, or null.</param>
        /// <param name="streamKind">Medium to keep - the caller always names exactly one.</param>
        /// <param name="liveOnly">True to keep only the sessions that have not ended.</param>
        /// <param name="subscriptionId">Subscription to keep, or null for every subscription.</param>
        /// <param name="ownerId">Owner of the subscription to keep, or null for every owner.</param>
        /// <param name="streamId">Opaque stream identifier to keep, or null for every stream.</param>
        /// <param name="ip">Address to keep, compared exactly, or null.</param>
        /// <param name="countryCode">Upper-case ISO alpha-2 country to keep, or null.</param>
        /// <param name="isp">Provider to keep, compared exactly, or null.</param>
        /// <param name="userAgent">User agent to keep, compared exactly, or null.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<ConnectionLogPage> GetPageAsync(
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
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes every row of the log, whatever the reader had filtered the screen to.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>How many connections were removed; zero when the table was already empty.</returns>
        Task<int> ClearAllAsync(CancellationToken cancellationToken = default);
    }
}
