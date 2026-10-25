using System.Globalization;
using System.Linq.Expressions;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Series.Commands;
using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Models;
using Fluxy.Core.Series.ReadModels;
using Fluxy.Core.Series.Repositories;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="ISeriesRepository"/>.
    /// </summary>
    /// <remarks>
    /// The page and the detail read project the two episode counts in the same query as the
    /// row, so a page of twenty series costs one query rather than twenty. Both counts are
    /// aggregates over <c>series_episodes</c>; nothing is stored on the series row.
    /// </remarks>
    internal sealed class SeriesRepository : ISeriesRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeriesRepository"/> class.
        /// </summary>
        /// <param name="context">Context the series rows are read from and staged in.</param>
        public SeriesRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<SeriesPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? categoryId,
            SeriesStatus? status,
            SeriesSortField sortBy,
            SeriesSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Series.AsNoTracking();

            if (categoryId is { } wantedCategory)
            {
                query = query.Where(entry => entry.CategoryId == wantedCategory);
            }

            if (status is { } wantedStatus)
            {
                query = query.Where(entry => entry.Status == wantedStatus);
            }

            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                query = query.Where(entry => entry.Name.ToLower().Contains(needle));
            }

            var total = await query.CountAsync(cancellationToken);

            query = Order(query, sortBy, sortOrder);

            var rows = await query
                .Select(entry => new StoredSeries
                {
                    Id = entry.Id,
                    Name = entry.Name,
                    Status = entry.Status,
                    CategoryId = entry.CategoryId,
                    CategoryName = entry.Category!.Name,
                    PosterUrl = entry.PosterUrl,
                    SeasonsCount = entry.Episodes
                        .Select(episode => (int?)episode.SeasonNumber)
                        .Max() ?? 0,
                    EpisodesCount = entry.Episodes.Count(),
                    ReleaseDate = entry.ReleaseDate,
                    Rating = entry.Rating
                })
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new SeriesPage
            {
                Items = rows.Select(ToListItem).ToList(),
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public async Task<SeriesDetail?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var row = await _context.Series
                .AsNoTracking()
                .Where(entry => entry.Id == id)
                .Select(entry => new StoredSeriesDetail
                {
                    Id = entry.Id,
                    Name = entry.Name,
                    Status = entry.Status,
                    CategoryId = entry.CategoryId,
                    CategoryName = entry.Category!.Name,
                    ImdbId = entry.ImdbId,
                    PosterUrl = entry.PosterUrl,
                    BackdropUrl = entry.BackdropUrl,
                    Plot = entry.Plot,
                    Cast = entry.Cast,
                    Director = entry.Director,
                    Genres = entry.Genres,
                    ReleaseDate = entry.ReleaseDate,
                    Rating = entry.Rating,
                    SeasonsCount = entry.Episodes
                        .Select(episode => (int?)episode.SeasonNumber)
                        .Max() ?? 0,
                    EpisodesCount = entry.Episodes.Count()
                })
                .FirstOrDefaultAsync(cancellationToken);

            return row is null ? null : ToDetail(row);
        }

        /// <inheritdoc />
        public async Task<SeriesItem?> FindForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var series = await _context.Series
                .Include(entry => entry.Category)
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            return series?.ToModel();
        }

        /// <inheritdoc />
        public Task<bool> CategoryIsUsableAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default)
            => _context.ContentCategories
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Id == categoryId && entry.Type == ContentType.Series,
                    cancellationToken);

        /// <inheritdoc />
        public Task<bool> IsNameTakenAsync(
            string name,
            Guid categoryId,
            Guid? exceptSeries,
            CancellationToken cancellationToken = default)
            => _context.Series
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Name == name
                        && entry.CategoryId == categoryId
                        && (exceptSeries == null || entry.Id != exceptSeries),
                    cancellationToken);

        /// <inheritdoc />
        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.Series
                .AsNoTracking()
                .AnyAsync(entry => entry.Id == id, cancellationToken);

        /// <inheritdoc />
        public Task<Guid> AddAsync(
            SeriesWrite series,
            CancellationToken cancellationToken = default)
        {
            var entity = BuildEntity(series);
            _context.Series.Add(entity);

            return Task.FromResult(entity.Id);
        }

        /// <inheritdoc />
        public async Task<bool> UpdateAsync(
            Guid id,
            SeriesWrite series,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Series.FindAsync([id], cancellationToken);

            if (entity is null)
            {
                return false;
            }

            entity.Name = series.Name;
            entity.Status = series.Status;
            entity.CategoryId = series.CategoryId;
            entity.ImdbId = series.ImdbId;
            entity.PosterUrl = series.PosterUrl;
            entity.BackdropUrl = series.BackdropUrl;
            entity.Plot = series.Plot;
            entity.Cast = series.Cast.ToArray();
            entity.Director = series.Director;
            entity.Genres = series.Genres.ToArray();
            entity.ReleaseDate = series.ReleaseDate;
            entity.Rating = series.Rating;

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> RemoveAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Series.FindAsync([id], cancellationToken);

            if (entity is null)
            {
                return false;
            }

            _context.Series.Remove(entity);
            return true;
        }

        /// <summary>Builds one tracked row from the values a write resolved.</summary>
        private static SeriesEntity BuildEntity(SeriesWrite series) => new()
        {
            Name = series.Name,
            Status = series.Status,
            CategoryId = series.CategoryId,
            ImdbId = series.ImdbId,
            PosterUrl = series.PosterUrl,
            BackdropUrl = series.BackdropUrl,
            Plot = series.Plot,
            Cast = series.Cast.ToArray(),
            Director = series.Director,
            Genres = series.Genres.ToArray(),
            ReleaseDate = series.ReleaseDate,
            Rating = series.Rating
        };

        private static IQueryable<SeriesEntity> Order(
            IQueryable<SeriesEntity> query,
            SeriesSortField field,
            SeriesSortOrder order)
        {
            var descending = order is SeriesSortOrder.Descending;

            return field switch
            {
                SeriesSortField.Category => Chain(
                    query, entry => entry.Category!.Name, entry => entry.Name, descending),

                SeriesSortField.Status => Chain(
                    query, entry => entry.Status, entry => entry.Name, descending),

                SeriesSortField.Seasons => Chain(
                    query,
                    entry => entry.Episodes.Select(episode => (int?)episode.SeasonNumber).Max() ?? 0,
                    entry => entry.Name,
                    descending),

                SeriesSortField.Episodes => Chain(
                    query, entry => entry.Episodes.Count(), entry => entry.Name, descending),

                SeriesSortField.ReleaseDate => ChainNullsLast(
                    query, entry => entry.ReleaseDate, entry => entry.Name, descending),

                SeriesSortField.Rating => ChainNullsLast(
                    query, entry => entry.Rating, entry => entry.Name, descending),

                _ => Chain(query, entry => entry.Name, entry => entry.Id, descending)
            };
        }

        private static IOrderedQueryable<SeriesEntity> Chain<TKey1, TKey2>(
            IQueryable<SeriesEntity> source,
            Expression<Func<SeriesEntity, TKey1>> primary,
            Expression<Func<SeriesEntity, TKey2>> tieBreak,
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
        private static IOrderedQueryable<SeriesEntity> ChainNullsLast<TKey1, TKey2>(
            IQueryable<SeriesEntity> source,
            Expression<Func<SeriesEntity, TKey1>> primary,
            Expression<Func<SeriesEntity, TKey2>> tieBreak,
            bool descending)
        {
            var isNull = Expression.Lambda<Func<SeriesEntity, bool>>(
                Expression.Equal(
                    primary.Body,
                    Expression.Constant(null, primary.Body.Type)),
                primary.Parameters);

            var withGrouped = source.OrderBy(isNull);

            return descending
                ? withGrouped.ThenByDescending(primary).ThenByDescending(tieBreak)
                : withGrouped.ThenBy(primary).ThenBy(tieBreak);
        }

        private static SeriesListItem ToListItem(StoredSeries row) => new()
        {
            Id = row.Id,
            Name = row.Name,
            Status = row.Status,
            CategoryId = row.CategoryId,
            CategoryName = row.CategoryName,
            PosterUrl = row.PosterUrl,
            SeasonsCount = row.SeasonsCount,
            EpisodesCount = row.EpisodesCount,
            ReleaseDate = row.ReleaseDate,
            Rating = row.Rating
        };

        private static SeriesDetail ToDetail(StoredSeriesDetail row) => new()
        {
            Id = row.Id,
            Name = row.Name,
            Status = row.Status,
            CategoryId = row.CategoryId,
            CategoryName = row.CategoryName,
            ImdbId = row.ImdbId,
            PosterUrl = row.PosterUrl,
            BackdropUrl = row.BackdropUrl,
            Plot = row.Plot,
            Cast = row.Cast,
            Director = row.Director,
            Genres = row.Genres,
            ReleaseDate = row.ReleaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Rating = row.Rating,
            SeasonsCount = row.SeasonsCount,
            EpisodesCount = row.EpisodesCount
        };

        /// <summary>The columns a page of the table draws, before the shape it returns.</summary>
        private sealed class StoredSeries
        {
            public Guid Id { get; init; }
            public string Name { get; init; } = string.Empty;
            public SeriesStatus Status { get; init; }
            public Guid CategoryId { get; init; }
            public string CategoryName { get; init; } = string.Empty;
            public string? PosterUrl { get; init; }
            public int SeasonsCount { get; init; }
            public int EpisodesCount { get; init; }
            public DateOnly? ReleaseDate { get; init; }
            public decimal? Rating { get; init; }
        }

        /// <summary>The columns a detail read draws, including the two episode aggregates.</summary>
        private sealed class StoredSeriesDetail
        {
            public Guid Id { get; init; }
            public string Name { get; init; } = string.Empty;
            public SeriesStatus Status { get; init; }
            public Guid CategoryId { get; init; }
            public string CategoryName { get; init; } = string.Empty;
            public string? ImdbId { get; init; }
            public string? PosterUrl { get; init; }
            public string? BackdropUrl { get; init; }
            public string? Plot { get; init; }
            public string[] Cast { get; init; } = [];
            public string? Director { get; init; }
            public string[] Genres { get; init; } = [];
            public DateOnly? ReleaseDate { get; init; }
            public decimal? Rating { get; init; }
            public int SeasonsCount { get; init; }
            public int EpisodesCount { get; init; }
        }
    }
}
