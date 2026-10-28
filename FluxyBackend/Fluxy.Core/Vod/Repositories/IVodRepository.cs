using Fluxy.Core.Vod.Commands;
using Fluxy.Core.Vod.Enums;
using Fluxy.Core.Vod.Models;
using Fluxy.Core.Vod.ReadModels;
using Fluxy.Core.Vod.Rows;

namespace Fluxy.Core.Vod.Repositories
{
    /// <summary>
    /// Reads and writes the on-demand items and the metadata stored on their rows.
    /// </summary>
    /// <remarks>
    /// The repository owns the page query, the category-scoped name probe and the storage of
    /// the two arrays. The service owns what an item may contain: which values are usable, how
    /// a patch resolves "keep" against "clear", and which refusal an outcome deserves.
    /// </remarks>
    public interface IVodRepository
    {
        /// <summary>Reads one filtered page of the table, counted before paging.</summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many items one page holds.</param>
        /// <param name="search">Case-insensitive substring of the name, or null.</param>
        /// <param name="categoryId">Category to keep, or null for every category.</param>
        /// <param name="status">State to keep, or null for both.</param>
        /// <param name="type">Kind to keep, or null for both.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<VodPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? categoryId,
            VodStatus? status,
            VodType? type,
            VodSortField sortBy,
            VodSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>Reads one item in full.</summary>
        /// <param name="id">Identifier of the item.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<VodItem?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds one item for a change, with the row left tracked. There are no child lists to
        /// load: the cast and the genres are columns on the row.
        /// </summary>
        /// <param name="id">Identifier of the item.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<VodItem?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Whether the identifier names an existing category of on-demand video.</summary>
        /// <param name="categoryId">Identifier of the category.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> CategoryIsUsableAsync(Guid categoryId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Which of the named videos exist.
        /// </summary>
        /// <remarks>
        /// The probe behind a playlist's entries. The membership rows carry a real foreign key,
        /// so a video that does not exist would be refused at the write anyway - this is what
        /// turns that refusal into a sentence under the field the operator can fix.
        /// </remarks>
        /// <param name="ids">Identifiers to test.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The subset of <paramref name="ids"/> that names a video.</returns>
        Task<IReadOnlySet<Guid>> GetExistingIdsAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken = default);

        /// <summary>Whether another item of the same category already holds the name.</summary>
        /// <param name="name">Name to probe, compared case sensitively.</param>
        /// <param name="categoryId">Category the name is scoped to.</param>
        /// <param name="exceptItem">Item to compare against itself, or null for a create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> IsNameTakenAsync(
            string name,
            Guid categoryId,
            Guid? exceptItem,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads every existing item of one category whose name is in a proposed set, in one
        /// query.
        /// </summary>
        /// <param name="categoryId">Category the names are scoped to.</param>
        /// <param name="names">Names to look for.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<IReadOnlyList<VodImportRow>> GetImportRowsAsync(
            Guid categoryId,
            IReadOnlyCollection<string> names,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a new item in its category.</summary>
        /// <param name="item">Values the row is written with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Identifier the new row carries.</returns>
        Task<Guid> AddAsync(VodItemWrite item, CancellationToken cancellationToken = default);

        /// <summary>Stages a batch of new items, left for the caller's single save.</summary>
        /// <param name="items">Values the rows are written with, in playlist order.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task AddRangeAsync(
            IReadOnlyList<VodItemWrite> items,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a change of the item's fields.</summary>
        /// <param name="id">Identifier of the item read for update.</param>
        /// <param name="item">Values the row must carry after the change.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>False when no row carries the identifier.</returns>
        Task<bool> UpdateAsync(
            Guid id,
            VodItemWrite item,
            CancellationToken cancellationToken = default);

        /// <summary>Stages the removal of an item read for update.</summary>
        /// <param name="id">Identifier of the item.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>False when no row carries the identifier.</returns>
        Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Stages a status on one item.</summary>
        /// <param name="id">Identifier of the item.</param>
        /// <param name="status">State the row must carry.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The previous status when the row exists, or null when it does not. The caller saves
        /// only when the returned value differs from <paramref name="status"/>.
        /// </returns>
        Task<VodStatus?> SetStatusAsync(
            Guid id,
            VodStatus status,
            CancellationToken cancellationToken = default);
    }
}
