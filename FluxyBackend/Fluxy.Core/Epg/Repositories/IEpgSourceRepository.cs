using Fluxy.Core.Epg.Commands;
using Fluxy.Core.Epg.Enums;
using Fluxy.Core.Epg.Models;
using Fluxy.Core.Epg.ReadModels;

namespace Fluxy.Core.Epg.Repositories
{
    /// <summary>
    /// Reads and writes the programme guide sources, and answers the one question about the
    /// streams that use them.
    /// </summary>
    /// <remarks>
    /// The repository owns the page queries and the storage of a row. The service owns what a
    /// source may contain and which refusal an outcome deserves. The channels read lives here
    /// although it queries the streams table, for the same reason the category repository
    /// counts content: the question is asked by this resource about its own rows, and an
    /// interface never leaves this layer.
    /// </remarks>
    public interface IEpgSourceRepository
    {
        /// <summary>Reads one filtered page of the table, counted before paging.</summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many sources one page holds.</param>
        /// <param name="search">Case-insensitive substring of the name or the address, or null.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<EpgSourcePage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            EpgSourceSortField sortBy,
            EpgSourceSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>Reads one source in full, raw guide excluded.</summary>
        /// <param name="id">Identifier of the source.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<EpgSource?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads one page of the streams that read their guide from a source.
        /// </summary>
        /// <param name="sourceId">Identifier of the source.</param>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many channels one page holds.</param>
        /// <param name="search">Case-insensitive substring of the stream name or the key, or null.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<EpgSourceChannelPage> GetChannelsPageAsync(
            Guid sourceId,
            int page,
            int pageSize,
            string? search,
            EpgSourceChannelSortField sortBy,
            EpgSourceChannelSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>Whether another source already holds the name.</summary>
        /// <param name="name">Name to probe, compared case sensitively.</param>
        /// <param name="exceptSource">Source to compare against itself, or null for a create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> IsNameTakenAsync(
            string name,
            Guid? exceptSource,
            CancellationToken cancellationToken = default);

        /// <summary>Whether the identifier names an existing source.</summary>
        /// <param name="id">Identifier to probe.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>How many configured streams read their guide from the source.</summary>
        /// <param name="sourceId">Identifier of the source.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<int> CountStreamsUsingAsync(Guid sourceId, CancellationToken cancellationToken = default);

        /// <summary>Stages a new source.</summary>
        /// <param name="source">Values the row is written with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Identifier the new row carries.</returns>
        Task<Guid> AddAsync(EpgSourceWrite source, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a change to the operator-owned fields. The raw guide and the moment of the
        /// last refresh are left exactly as they are.
        /// </summary>
        /// <param name="id">Identifier of the source.</param>
        /// <param name="source">Values the row must carry after the change.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>False when no row carries the identifier.</returns>
        Task<bool> UpdateAsync(
            Guid id,
            EpgSourceWrite source,
            CancellationToken cancellationToken = default);

        /// <summary>Stages the removal of a source. Its programme rows go with it.</summary>
        /// <param name="id">Identifier of the source.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>False when no row carries the identifier.</returns>
        Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
