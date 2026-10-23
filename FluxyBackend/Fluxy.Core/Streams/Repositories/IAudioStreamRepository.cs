using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Models;
using Fluxy.Core.Streams.ReadModels;
using Fluxy.Core.Streams.Rows;

namespace Fluxy.Core.Streams.Repositories
{
    /// <summary>
    /// The data access contract of the audio stream aggregate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A repository speaks the model and the projections, never an <c>IQueryable</c> and never
    /// an entity: the API layer names this interface, and the implementation is the only place
    /// that knows a table exists. Reads return what the caller needs; writes are intent
    /// methods that stage a change for the caller's single save.
    /// </para>
    /// <para>
    /// The source list is rows of its own. The detail read fetches it with one query; a page
    /// never asks for it, because twenty rows would otherwise cost twenty more reads for data
    /// the table does not draw. The update read does load it, because deciding which rows to
    /// remove is a comparison against what the row already holds.
    /// </para>
    /// </remarks>
    public interface IAudioStreamRepository
    {
        /// <summary>Reads one page of the audio streams table.</summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many streams one page holds.</param>
        /// <param name="search">Case-insensitive substring of the name, or null.</param>
        /// <param name="categoryId">Category to keep, or null for every category.</param>
        /// <param name="status">State to keep, or null for both.</param>
        /// <param name="type">Kind to keep, or null for both.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<AudioStreamPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? categoryId,
            AudioStreamStatus? status,
            AudioStreamType? type,
            AudioStreamSortField sortBy,
            AudioStreamSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>Reads one stream in full, including its sources.</summary>
        /// <param name="id">Identifier of the stream.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<AudioStream?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds one stream for a change, with its sources loaded and the row left tracked.
        /// </summary>
        /// <param name="id">Identifier of the stream.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<AudioStream?> FindForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        /// <summary>Whether the identifier names an existing category of audio streams.</summary>
        /// <param name="categoryId">Identifier of the category.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> CategoryIsUsableAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default);

        /// <summary>Whether another stream of the same category already holds the name.</summary>
        /// <param name="name">Name to probe, compared case sensitively.</param>
        /// <param name="categoryId">Category the name is scoped to.</param>
        /// <param name="exceptStream">Stream to compare against itself, or null for a create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> IsNameTakenAsync(
            string name,
            Guid categoryId,
            Guid? exceptStream,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads every existing stream of one category whose name is in a proposed set, in one
        /// query.
        /// </summary>
        /// <param name="categoryId">Category the names are scoped to.</param>
        /// <param name="names">Names to look for.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Existing streams the category holds under any of <paramref name="names"/>.</returns>
        Task<IReadOnlyList<AudioStreamImportRow>> GetImportRowsAsync(
            Guid categoryId,
            IReadOnlyCollection<string> names,
            CancellationToken cancellationToken = default);

        /// <summary>Stages changes on several existing streams at once.</summary>
        /// <param name="updates">Changes to stage, one per existing stream.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task UpdateRangeAsync(
            IReadOnlyList<AudioStreamBatchUpdate> updates,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a new stream in its category.</summary>
        /// <param name="stream">Values the row is written with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Identifier the new row carries.</returns>
        Task<Guid> AddAsync(
            AudioStreamWrite stream,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a batch of new streams in one category.</summary>
        /// <param name="streams">Values the rows are written with, in playlist order.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task AddRangeAsync(
            IReadOnlyList<AudioStreamWrite> streams,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a change of the stream's own fields and its sources.</summary>
        /// <param name="id">Identifier of the stream read for update.</param>
        /// <param name="stream">Values the row must carry after the change.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>False when no row carries the identifier.</returns>
        Task<bool> UpdateAsync(
            Guid id,
            AudioStreamWrite stream,
            CancellationToken cancellationToken = default);

        /// <summary>Stages the removal of a stream read for update.</summary>
        /// <param name="id">Identifier of the stream.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>False when no row carries the identifier.</returns>
        Task<bool> RemoveAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a status on a stream, without touching the reported active source.</summary>
        /// <param name="id">Identifier of the stream.</param>
        /// <param name="status">State the row must carry.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The previous status when the row exists, or null when it does not. The caller saves
        /// only when the returned value differs from <paramref name="status"/>.
        /// </returns>
        Task<AudioStreamStatus?> SetStatusAsync(
            Guid id,
            AudioStreamStatus status,
            CancellationToken cancellationToken = default);
    }
}
