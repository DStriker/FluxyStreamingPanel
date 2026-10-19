using Fluxy.Core.Abstractions.Repositories;
using Fluxy.Core.Models.Content;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IContentCategoryRepository"/>.
    /// </summary>
    /// <remarks>
    /// The list read keeps the order the indexes were designed for: a type's rows by position,
    /// with creation time and identifier as tie-breaks. The tie-breaks matter because a
    /// deletion leaves a gap and a raced create can share a position; without them the database
    /// is free to answer a different order on every read.
    /// </remarks>
    public sealed class ContentCategoryRepository : IContentCategoryRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentCategoryRepository"/> class.
        /// </summary>
        /// <param name="context">Context the category rows are read from and staged in.</param>
        public ContentCategoryRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ContentCategory>> GetByTypeAsync(
            ContentType type,
            CancellationToken cancellationToken = default)
        {
            var rows = await _context.ContentCategories
                .AsNoTracking()
                .Where(entry => entry.Type == type)
                .OrderBy(entry => entry.SortOrder)
                .ThenBy(entry => entry.CreatedAt)
                .ThenBy(entry => entry.Id)
                .ToListAsync(cancellationToken);

            return rows.Select(row => row.ToModel()).ToList();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ContentCategory>> GetTrackedByTypeAsync(
            ContentType type,
            CancellationToken cancellationToken = default)
        {
            var rows = await _context.ContentCategories
                .Where(entry => entry.Type == type)
                .OrderBy(entry => entry.SortOrder)
                .ThenBy(entry => entry.CreatedAt)
                .ThenBy(entry => entry.Id)
                .ToListAsync(cancellationToken);

            return rows.Select(row => row.ToModel()).ToList();
        }

        /// <inheritdoc />
        public async Task<ContentCategory?> FindForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.ContentCategories
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            return entity?.ToModel();
        }

        /// <inheritdoc />
        public Task<bool> IsNameTakenAsync(
            string name,
            ContentType type,
            Guid? exceptCategory,
            CancellationToken cancellationToken = default)
            => _context.ContentCategories
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Name == name
                        && entry.Type == type
                        && (exceptCategory == null || entry.Id != exceptCategory),
                    cancellationToken);

        /// <inheritdoc />
        public async Task<int> GetLastSortOrderAsync(
            ContentType type,
            CancellationToken cancellationToken = default)
            => await _context.ContentCategories
                .Where(entry => entry.Type == type)
                .MaxAsync(entry => (int?)entry.SortOrder, cancellationToken) ?? 0;

        /// <inheritdoc />
        public Task<Guid> AddAsync(
            NewContentCategory category,
            int sortOrder,
            CancellationToken cancellationToken = default)
        {
            var entity = new ContentCategoryEntity
            {
                Name = category.Name,
                Type = category.Type,
                SortOrder = sortOrder
            };

            _context.ContentCategories.Add(entity);
            return Task.FromResult(entity.Id);
        }

        /// <inheritdoc />
        public async Task SetNameAsync(
            Guid id,
            string name,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.ContentCategories.FindAsync([id], cancellationToken);
            if (entity is not null)
            {
                entity.Name = name;
            }
        }

        /// <inheritdoc />
        public async Task SetSortOrderAsync(
            Guid id,
            int sortOrder,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.ContentCategories.FindAsync([id], cancellationToken);
            if (entity is not null)
            {
                entity.SortOrder = sortOrder;
            }
        }

        /// <inheritdoc />
        public async Task RemoveAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.ContentCategories.FindAsync([id], cancellationToken);
            if (entity is not null)
            {
                _context.ContentCategories.Remove(entity);
            }
        }

        /// <inheritdoc />
        public Task<int> CountStreamsAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default)
            => _context.VideoStreams
                .AsNoTracking()
                .CountAsync(entry => entry.CategoryId == categoryId, cancellationToken);
    }
}
