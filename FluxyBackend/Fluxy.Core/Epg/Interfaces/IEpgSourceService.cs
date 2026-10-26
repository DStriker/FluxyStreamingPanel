using Fluxy.Core.Epg.Commands;
using Fluxy.Core.Epg.Enums;
using Fluxy.Core.Epg.Outcomes;
using Fluxy.Core.Epg.ReadModels;

namespace Fluxy.Core.Epg.Interfaces
{
    /// <summary>
    /// Reads the programme guide sources and changes them on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// The guide updater is not part of this interface: nothing here writes a raw guide or a
    /// programme row, because that machinery arrives with the update logic. What is here is the
    /// operator's side of the domain - the sources, their retention and the streams that use
    /// them.
    /// </remarks>
    public interface IEpgSourceService
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

        /// <summary>Reads one source as the API answers with it, without its raw guide.</summary>
        /// <param name="id">Identifier of the source.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The source, or null when there is no such one.</returns>
        Task<EpgSourceView?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads one page of the configured streams that read their guide from a source.
        /// </summary>
        /// <param name="sourceId">Identifier of the source.</param>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many channels one page holds.</param>
        /// <param name="search">Case-insensitive substring of the stream name or the key, or null.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The page, or null when no source carries the identifier - which is a different
        /// answer from a source with no channels.
        /// </returns>
        Task<EpgSourceChannelPage?> GetChannelsAsync(
            Guid sourceId,
            int page,
            int pageSize,
            string? search,
            EpgSourceChannelSortField sortBy,
            EpgSourceChannelSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>Creates a source.</summary>
        /// <param name="input">Name, address and retention.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, carrying the identifier once the row exists.</returns>
        Task<EpgSourceOutcome> CreateAsync(
            NewEpgSource input,
            CancellationToken cancellationToken = default);

        /// <summary>Changes a source. Every field a body can name is optional.</summary>
        /// <param name="id">Identifier of the source to change.</param>
        /// <param name="patch">What to change about it.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<EpgSourceOutcome> UpdateAsync(
            Guid id,
            EpgSourcePatch patch,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a source. A source a stream still reads its guide from is refused; the
        /// programmes filed under it go with it.
        /// </summary>
        /// <param name="id">Identifier of the source to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<EpgSourceOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
