using System.Linq.Expressions;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Vod.Commands;
using Fluxy.Core.Vod.Enums;
using Fluxy.Core.Vod.Models;
using Fluxy.Core.Vod.ReadModels;
using Fluxy.Core.Vod.Repositories;
using Fluxy.Core.Vod.Rows;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IVodRepository"/>.
    /// </summary>
    /// <remarks>
    /// The detail read includes the category for the name the form draws; a page projects
    /// that name into the row so twenty items cost one query rather than twenty. The update
    /// read includes it too, because the merged model is built from the row the change is
    /// about to become.
    /// </remarks>
    internal sealed class VodRepository : IVodRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="VodRepository"/> class.
        /// </summary>
        /// <param name="context">Context the item rows are read from and staged in.</param>
        public VodRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<VodPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? categoryId,
            VodStatus? status,
            VodType? type,
            VodSortField sortBy,
            VodSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            var query = _context.VodItems.AsNoTracking();

            if (categoryId is { } wantedCategory)
            {
                query = query.Where(entry => entry.CategoryId == wantedCategory);
            }

            if (status is { } wantedStatus)
            {
                query = query.Where(entry => entry.Status == wantedStatus);
            }

            if (type is { } wantedType)
            {
                query = query.Where(entry => entry.Type == wantedType);
            }

            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                query = query.Where(entry => entry.Name.ToLower().Contains(needle));
            }

            var total = await query.CountAsync(cancellationToken);

            query = Order(query, sortBy, sortOrder);

            var rows = await query
                .Select(entry => new StoredVod
                {
                    Id = entry.Id,
                    Name = entry.Name,
                    Status = entry.Status,
                    Type = entry.Type,
                    CategoryId = entry.CategoryId,
                    CategoryName = entry.Category!.Name,
                    PosterUrl = entry.PosterUrl,
                    ReleaseDate = entry.ReleaseDate,
                    Rating = entry.Rating
                })
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new VodPage
            {
                Items = rows.Select(ToListItem).ToList(),
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public async Task<VodItem?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var item = await _context.VodItems
                .AsNoTracking()
                .Include(entry => entry.Category)
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            return item?.ToModel();
        }

        /// <inheritdoc />
        public async Task<VodItem?> FindForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var item = await _context.VodItems
                .Include(entry => entry.Category)
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            return item?.ToModel();
        }

        /// <inheritdoc />
        public Task<bool> CategoryIsUsableAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default)
            => _context.ContentCategories
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Id == categoryId && entry.Type == ContentType.VideoOnDemand,
                    cancellationToken);

        /// <inheritdoc />
        public async Task<IReadOnlySet<Guid>> GetExistingIdsAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken = default)
        {
            if (ids.Count == 0)
            {
                return new HashSet<Guid>();
            }

            var wanted = ids.Distinct().ToList();

            var found = await _context.VodItems
                .AsNoTracking()
                .Where(entry => wanted.Contains(entry.Id))
                .Select(entry => entry.Id)
                .ToListAsync(cancellationToken);

            return found.ToHashSet();
        }

        /// <inheritdoc />
        public Task<bool> IsNameTakenAsync(
            string name,
            Guid categoryId,
            Guid? exceptItem,
            CancellationToken cancellationToken = default)
            => _context.VodItems
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Name == name
                        && entry.CategoryId == categoryId
                        && (exceptItem == null || entry.Id != exceptItem),
                    cancellationToken);

        /// <inheritdoc />
        public async Task<IReadOnlyList<VodImportRow>> GetImportRowsAsync(
            Guid categoryId,
            IReadOnlyCollection<string> names,
            CancellationToken cancellationToken = default)
        {
            if (names.Count == 0)
            {
                return [];
            }

            var wanted = names.ToArray();

            return await _context.VodItems
                .AsNoTracking()
                .Where(entry => entry.CategoryId == categoryId && wanted.Contains(entry.Name))
                .Select(entry => new VodImportRow
                {
                    Id = entry.Id,
                    Name = entry.Name
                })
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<Guid> AddAsync(
            VodItemWrite item,
            CancellationToken cancellationToken = default)
        {
            var entity = BuildEntity(item);
            _context.VodItems.Add(entity);

            return Task.FromResult(entity.Id);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<Guid>> AddRangeAsync(
            IReadOnlyList<VodItemWrite> items,
            CancellationToken cancellationToken = default)
        {
            var created = new List<Guid>(items.Count);

            foreach (var item in items)
            {
                var entity = BuildEntity(item);

                _context.VodItems.Add(entity);
                created.Add(entity.Id);
            }

            return Task.FromResult<IReadOnlyList<Guid>>(created);
        }

        /// <inheritdoc />
        public async Task<bool> UpdateAsync(
            Guid id,
            VodItemWrite item,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.VodItems.FindAsync([id], cancellationToken);

            if (entity is null)
            {
                return false;
            }

            entity.Name = item.Name;
            entity.Status = item.Status;
            entity.Type = item.Type;
            entity.CategoryId = item.CategoryId;
            entity.Source = item.Source;
            entity.SubtitleUrl = item.SubtitleUrl;
            entity.ImdbId = item.ImdbId;
            entity.PosterUrl = item.PosterUrl;
            entity.BackdropUrl = item.BackdropUrl;
            entity.Plot = item.Plot;
            entity.Cast = item.Cast.ToArray();
            entity.Director = item.Director;
            entity.Genres = item.Genres.ToArray();
            entity.ReleaseDate = item.ReleaseDate;
            entity.Rating = item.Rating;
            entity.CountryCode = item.CountryCode;

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> RemoveAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.VodItems.FindAsync([id], cancellationToken);

            if (entity is null)
            {
                return false;
            }

            _context.VodItems.Remove(entity);
            return true;
        }

        /// <inheritdoc />
        public async Task<VodStatus?> SetStatusAsync(
            Guid id,
            VodStatus status,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.VodItems.FindAsync([id], cancellationToken);

            if (entity is null)
            {
                return null;
            }

            var previous = entity.Status;
            entity.Status = status;
            return previous;
        }

        /// <summary>Builds one tracked row from the values a write resolved.</summary>
        private static VodItemEntity BuildEntity(VodItemWrite item) => new()
        {
            Name = item.Name,
            Status = item.Status,
            Type = item.Type,
            CategoryId = item.CategoryId,
            Source = item.Source,
            SubtitleUrl = item.SubtitleUrl,
            ImdbId = item.ImdbId,
            PosterUrl = item.PosterUrl,
            BackdropUrl = item.BackdropUrl,
            Plot = item.Plot,
            Cast = item.Cast.ToArray(),
            Director = item.Director,
            Genres = item.Genres.ToArray(),
            ReleaseDate = item.ReleaseDate,
            Rating = item.Rating,
            CountryCode = item.CountryCode
        };

        private static IQueryable<VodItemEntity> Order(
            IQueryable<VodItemEntity> query,
            VodSortField field,
            VodSortOrder order)
        {
            var descending = order is VodSortOrder.Descending;

            return field switch
            {
                VodSortField.Category => Chain(
                    query, entry => entry.Category!.Name, entry => entry.Name, descending),

                VodSortField.Status => Chain(
                    query, entry => entry.Status, entry => entry.Name, descending),

                VodSortField.Type => Chain(
                    query, entry => entry.Type, entry => entry.Name, descending),

                // Both nullable columns group their nulls last in either direction: a page
                // ordered by rating must not open with the items that have none.
                VodSortField.ReleaseDate => ChainNullsLast(
                    query, entry => entry.ReleaseDate, entry => entry.Name, descending),

                VodSortField.Rating => ChainNullsLast(
                    query, entry => entry.Rating, entry => entry.Name, descending),

                _ => Chain(query, entry => entry.Name, entry => entry.Id, descending)
            };
        }

        private static IOrderedQueryable<VodItemEntity> Chain<TKey1, TKey2>(
            IQueryable<VodItemEntity> source,
            Expression<Func<VodItemEntity, TKey1>> primary,
            Expression<Func<VodItemEntity, TKey2>> tieBreak,
            bool descending)
        {
            var ordered = descending
                ? source.OrderByDescending(primary)
                : source.OrderBy(primary);

            return descending
                ? ordered.ThenByDescending(tieBreak)
                : ordered.ThenBy(tieBreak);
        }

        /// <summary>Like <see cref="Chain"/>, except that rows without a value go last.</summary>
        private static IOrderedQueryable<VodItemEntity> ChainNullsLast<TKey1, TKey2>(
            IQueryable<VodItemEntity> source,
            Expression<Func<VodItemEntity, TKey1>> primary,
            Expression<Func<VodItemEntity, TKey2>> tieBreak,
            bool descending)
        {
            var isNull = Expression.Lambda<Func<VodItemEntity, bool>>(
                Expression.Equal(
                    primary.Body,
                    Expression.Constant(null, primary.Body.Type)),
                primary.Parameters);

            var withGrouped = source.OrderBy(isNull);

            return descending
                ? withGrouped.ThenByDescending(primary).ThenByDescending(tieBreak)
                : withGrouped.ThenBy(primary).ThenBy(tieBreak);
        }

        private static VodListItem ToListItem(StoredVod row) => new()
        {
            Id = row.Id,
            Name = row.Name,
            Status = row.Status,
            Type = row.Type,
            CategoryId = row.CategoryId,
            CategoryName = row.CategoryName,
            PosterUrl = row.PosterUrl,
            ReleaseDate = row.ReleaseDate,
            Rating = row.Rating
        };

        /// <summary>The columns a page of the table draws, before the shape it returns.</summary>
        private sealed class StoredVod
        {
            public Guid Id { get; init; }
            public string Name { get; init; } = string.Empty;
            public VodStatus Status { get; init; }
            public VodType Type { get; init; }
            public Guid CategoryId { get; init; }
            public string CategoryName { get; init; } = string.Empty;
            public string? PosterUrl { get; init; }
            public DateOnly? ReleaseDate { get; init; }
            public decimal? Rating { get; init; }
        }
    }
}
