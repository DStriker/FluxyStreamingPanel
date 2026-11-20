using Fluxy.Core.Servers.Commands;
using Fluxy.Core.Servers.Enums;
using Fluxy.Core.Servers.Models;
using Fluxy.Core.Servers.ReadModels;

namespace Fluxy.Core.Servers.Repositories
{
    /// <summary>
    /// Reads and writes the server rows, and answers the one question about the main row that a
    /// seeder asks before it writes anything.
    /// </summary>
    /// <remarks>
    /// The repository owns the page query - including the filter it is counted under - and the
    /// storage of a row. The service owns what a server may contain, which refusal an outcome
    /// deserves, and whether the row is the main server. No query object leaves this layer.
    /// </remarks>
    public interface IServerRepository
    {
        /// <summary>
        /// Reads one filtered page of the table, counted before paging. The filter is the
        /// search, applied before the count so <see cref="ServerPage.Total"/> describes the
        /// filtered list rather than the page.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many servers one page holds.</param>
        /// <param name="search">
        /// Case-insensitive substring of the name, the host name or the address, or null.
        /// </param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<ServerPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            ServerSortField sortBy,
            ServerSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>Reads one server in full.</summary>
        /// <param name="id">Identifier of the server.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The server, or null when no row carries the identifier.</returns>
        Task<Server?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Whether the main server row exists.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// Asked by the startup seeder, which creates the row only when it is absent - never by
        /// the write endpoints, whose delete guard reads the row it was asked about.
        /// </remarks>
        Task<bool> ExistsMainAsync(CancellationToken cancellationToken = default);

        /// <summary>Whether another server already holds the name.</summary>
        /// <param name="name">Name to probe, compared exactly.</param>
        /// <param name="exceptServer">Server to compare against itself, or null for a create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> IsNameTakenAsync(
            string name,
            Guid? exceptServer,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a new server.</summary>
        /// <param name="server">Values the row is written with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Identifier the new row carries.</returns>
        Task<Guid> AddAsync(ServerWrite server, CancellationToken cancellationToken = default);

        /// <summary>Stages a change to every editable field of a server.</summary>
        /// <param name="id">Identifier of the server.</param>
        /// <param name="server">Values the row must carry after the change.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>False when no row carries the identifier.</returns>
        Task<bool> UpdateAsync(
            Guid id,
            ServerWrite server,
            CancellationToken cancellationToken = default);

        /// <summary>Stages the removal of a server.</summary>
        /// <param name="id">Identifier of the server.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>False when no row carries the identifier.</returns>
        Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
