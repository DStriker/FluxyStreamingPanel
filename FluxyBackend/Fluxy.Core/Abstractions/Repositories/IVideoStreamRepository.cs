using Fluxy.Core.Models.Streams;

namespace Fluxy.Core.Abstractions.Repositories
{
    /// <summary>
    /// Reads and writes the configured streams and the two ordered lists stored beside them.
    /// </summary>
    /// <remarks>
    /// The repository owns the page query, the category-scoped name probe, the position a stream
    /// takes and the replacement of the two lists. The service owns what a stream may contain:
    /// which fields its type uses, when an archive or a guide is valid, and which refusal an
    /// outcome deserves.
    /// </remarks>
    public interface IVideoStreamRepository
    {
        /// <summary>
        /// Reads one filtered page of the streams table, counted before paging.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many streams one page holds.</param>
        /// <param name="search">Case-insensitive substring of the name, or null.</param>
        /// <param name="categoryId">Category to keep, or null for every category.</param>
        /// <param name="status">State to keep, or null for both.</param>
        /// <param name="type">Kind to keep, or null for every kind.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<VideoStreamPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? categoryId,
            VideoStreamStatus? status,
            VideoStreamType? type,
            VideoStreamSortField sortBy,
            VideoStreamSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>Reads one stream in full, including both lists.</summary>
        /// <param name="id">Identifier of the stream.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<VideoStream?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds one stream for a change, with both lists loaded and the row left tracked.
        /// </summary>
        /// <param name="id">Identifier of the stream.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<VideoStream?> FindForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Whether the identifier names an existing category of video streams.
        /// </summary>
        /// <param name="categoryId">Identifier of the category.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> CategoryIsUsableAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Whether another stream of the same category already holds the name.
        /// </summary>
        /// <param name="name">Name to probe, compared case sensitively.</param>
        /// <param name="categoryId">Category the name is scoped to.</param>
        /// <param name="exceptStream">Stream to compare against itself, or null for a create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> IsNameTakenAsync(
            string name,
            Guid categoryId,
            Guid? exceptStream,
            CancellationToken cancellationToken = default);

        /// <summary>Reads the highest position of a category, or zero when it is empty.</summary>
        /// <param name="categoryId">Identifier of the category.</param>
        /// <param name="exceptStream">Stream whose own position is not counted, or null.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<int> GetLastSortOrderAsync(
            Guid categoryId,
            Guid? exceptStream,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a new stream at the end of its category's list.</summary>
        /// <param name="stream">Values the row is written with.</param>
        /// <param name="sortOrder">Position the row must carry.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Identifier the new row carries.</returns>
        Task<Guid> AddAsync(
            VideoStreamWrite stream,
            int sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a change of the stream's own fields and both lists.
        /// </summary>
        /// <param name="id">Identifier of the stream read for update.</param>
        /// <param name="stream">Values the row must carry after the change.</param>
        /// <param name="sortOrder">
        /// Position to write when the stream moved category, or null to keep the current one.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>False when no row carries the identifier.</returns>
        Task<bool> UpdateAsync(
            Guid id,
            VideoStreamWrite stream,
            int? sortOrder,
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
        Task<VideoStreamStatus?> SetStatusAsync(
            Guid id,
            VideoStreamStatus status,
            CancellationToken cancellationToken = default);
    }
}
