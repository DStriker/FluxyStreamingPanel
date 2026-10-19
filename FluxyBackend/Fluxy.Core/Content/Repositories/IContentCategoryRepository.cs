using Fluxy.Core.Content.Commands;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Models;

namespace Fluxy.Core.Content.Repositories
{
    /// <summary>
    /// Reads and writes the categories content is filed under.
    /// </summary>
    /// <remarks>
    /// The repository owns the query shape of the resource - the order of a type's list, the
    /// unique-name probe and the position a new row takes - while the service owns the rules
    /// that decide whether a request may reach a write at all.
    /// </remarks>
    public interface IContentCategoryRepository
    {
        /// <summary>
        /// Reads one type's categories, lowest position first, with the same tie-breaks every
        /// read of the list uses.
        /// </summary>
        /// <param name="type">The kind of content whose categories are wanted.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<IReadOnlyList<ContentCategory>> GetByTypeAsync(
            ContentType type,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads one type's categories for a reorder, leaving the rows tracked.
        /// </summary>
        /// <param name="type">The kind of content whose categories are being reordered.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<IReadOnlyList<ContentCategory>> GetTrackedByTypeAsync(
            ContentType type,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds one category for a change, leaving its row tracked for the intent methods.
        /// </summary>
        /// <param name="id">Identifier of the category.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<ContentCategory?> FindForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Whether another category of the same type already holds the name.
        /// </summary>
        /// <param name="name">Name to probe, compared case sensitively.</param>
        /// <param name="type">Kind of content the name belongs to.</param>
        /// <param name="exceptCategory">Category to compare against itself, or null for a create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> IsNameTakenAsync(
            string name,
            ContentType type,
            Guid? exceptCategory,
            CancellationToken cancellationToken = default);

        /// <summary>Reads the highest position of one type, or zero when the type is empty.</summary>
        /// <param name="type">Kind of content whose end is wanted.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<int> GetLastSortOrderAsync(
            ContentType type,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a new category at the given position.</summary>
        /// <param name="category">Name and kind of the new category.</param>
        /// <param name="sortOrder">Position the row must carry.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Identifier the new row carries.</returns>
        Task<Guid> AddAsync(
            NewContentCategory category,
            int sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a new name on a category read for update.</summary>
        /// <param name="id">Identifier of the category.</param>
        /// <param name="name">Name the row must carry.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetNameAsync(
            Guid id,
            string name,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a position on a category read for update.</summary>
        /// <param name="id">Identifier of the category.</param>
        /// <param name="sortOrder">Position the row must carry.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetSortOrderAsync(
            Guid id,
            int sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>Stages the removal of a category read for update.</summary>
        /// <param name="id">Identifier of the category.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task RemoveAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        /// <summary>Counts the streams still filed under a category.</summary>
        /// <param name="categoryId">Identifier of the category.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<int> CountStreamsAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default);
    }
}
