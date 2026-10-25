using System.Linq.Expressions;
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
    /// EF Core implementation of <see cref="IEpisodeRepository"/>.
    /// </summary>
    /// <remarks>
    /// Every query is scoped to the series the request was made under, so an episode of a
    /// different series is not reachable by its bare identifier. The page query counts before
    /// paging, for the reason every other table does: the pager draws <c>Total</c>.
    /// </remarks>
    internal sealed class EpisodeRepository : IEpisodeRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="EpisodeRepository"/> class.
        /// </summary>
        /// <param name="context">Context the episode rows are read from and staged in.</param>
        public EpisodeRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<EpisodePage> GetPageAsync(
            Guid seriesId,
            int page,
            int pageSize,
            string? search,
            int? seasonNumber,
            EpisodeStatus? status,
            EpisodeType? type,
            EpisodeSortField sortBy,
            EpisodeSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Episodes
                .AsNoTracking()
                .Where(entry => entry.SeriesId == seriesId);

            if (seasonNumber is { } wantedSeason)
            {
                query = query.Where(entry => entry.SeasonNumber == wantedSeason);
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
                query = query.Where(entry =>
                    (entry.Name != null && entry.Name.ToLower().Contains(needle))
                    || entry.Source.ToLower().Contains(needle));
            }

            var total = await query.CountAsync(cancellationToken);

            query = Order(query, sortBy, sortOrder);

            var rows = await query
                .Select(entry => new StoredEpisode
                {
                    Id = entry.Id,
                    SeriesId = entry.SeriesId,
                    SeasonNumber = entry.SeasonNumber,
                    EpisodeNumber = entry.EpisodeNumber,
                    Name = entry.Name,
                    Status = entry.Status,
                    Type = entry.Type,
                    Source = entry.Source,
                    ImageUrl = entry.ImageUrl,
                    ReleaseDate = entry.ReleaseDate,
                    Rating = entry.Rating
                })
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new EpisodePage
            {
                Items = rows.Select(ToListItem).ToList(),
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public async Task<Episode?> GetAsync(
            Guid seriesId,
            Guid episodeId,
            CancellationToken cancellationToken = default)
        {
            var episode = await _context.Episodes
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    entry => entry.Id == episodeId && entry.SeriesId == seriesId,
                    cancellationToken);

            return episode?.ToModel();
        }

        /// <inheritdoc />
        public async Task<Episode?> FindForUpdateAsync(
            Guid seriesId,
            Guid episodeId,
            CancellationToken cancellationToken = default)
        {
            var episode = await _context.Episodes
                .FirstOrDefaultAsync(
                    entry => entry.Id == episodeId && entry.SeriesId == seriesId,
                    cancellationToken);

            return episode?.ToModel();
        }

        /// <inheritdoc />
        public Task<bool> SeasonExistsAsync(
            Guid seriesId,
            int seasonNumber,
            CancellationToken cancellationToken = default)
            => _context.Episodes
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.SeriesId == seriesId && entry.SeasonNumber == seasonNumber,
                    cancellationToken);

        /// <inheritdoc />
        public Task<bool> IsNumberTakenAsync(
            Guid seriesId,
            int seasonNumber,
            int episodeNumber,
            Guid? exceptEpisode,
            CancellationToken cancellationToken = default)
            => _context.Episodes
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.SeriesId == seriesId
                        && entry.SeasonNumber == seasonNumber
                        && entry.EpisodeNumber == episodeNumber
                        && (exceptEpisode == null || entry.Id != exceptEpisode),
                    cancellationToken);

        /// <inheritdoc />
        public Task<Guid> AddAsync(
            EpisodeWrite episode,
            CancellationToken cancellationToken = default)
        {
            var entity = BuildEntity(episode);
            _context.Episodes.Add(entity);

            return Task.FromResult(entity.Id);
        }

        /// <inheritdoc />
        public Task AddRangeAsync(
            IReadOnlyList<EpisodeWrite> episodes,
            CancellationToken cancellationToken = default)
        {
            foreach (var episode in episodes)
            {
                _context.Episodes.Add(BuildEntity(episode));
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task<bool> UpdateAsync(
            Guid seriesId,
            Guid episodeId,
            EpisodeWrite episode,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Episodes
                .FirstOrDefaultAsync(
                    entry => entry.Id == episodeId && entry.SeriesId == seriesId,
                    cancellationToken);

            if (entity is null)
            {
                return false;
            }

            entity.SeasonNumber = episode.SeasonNumber;
            entity.EpisodeNumber = episode.EpisodeNumber;
            entity.Name = episode.Name;
            entity.Source = episode.Source;
            entity.Status = episode.Status;
            entity.Type = episode.Type;
            entity.ImdbId = episode.ImdbId;
            entity.ImageUrl = episode.ImageUrl;
            entity.Plot = episode.Plot;
            entity.ReleaseDate = episode.ReleaseDate;
            entity.Rating = episode.Rating;

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> RemoveAsync(
            Guid seriesId,
            Guid episodeId,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Episodes
                .FirstOrDefaultAsync(
                    entry => entry.Id == episodeId && entry.SeriesId == seriesId,
                    cancellationToken);

            if (entity is null)
            {
                return false;
            }

            _context.Episodes.Remove(entity);
            return true;
        }

        /// <summary>Builds one tracked row from the values a write resolved.</summary>
        private static EpisodeEntity BuildEntity(EpisodeWrite episode) => new()
        {
            SeriesId = episode.SeriesId,
            SeasonNumber = episode.SeasonNumber,
            EpisodeNumber = episode.EpisodeNumber,
            Name = episode.Name,
            Source = episode.Source,
            Status = episode.Status,
            Type = episode.Type,
            ImdbId = episode.ImdbId,
            ImageUrl = episode.ImageUrl,
            Plot = episode.Plot,
            ReleaseDate = episode.ReleaseDate,
            Rating = episode.Rating
        };

        private static IQueryable<EpisodeEntity> Order(
            IQueryable<EpisodeEntity> query,
            EpisodeSortField field,
            EpisodeSortOrder order)
        {
            var descending = order is EpisodeSortOrder.Descending;

            return field switch
            {
                EpisodeSortField.Episode => Chain(
                    query,
                    entry => entry.EpisodeNumber,
                    entry => entry.SeasonNumber,
                    descending),

                EpisodeSortField.Name => ChainNullsLast(
                    query,
                    entry => entry.Name,
                    entry => entry.SeasonNumber,
                    descending,
                    entry => entry.EpisodeNumber),

                EpisodeSortField.Status => Chain(
                    query,
                    entry => entry.Status,
                    entry => entry.SeasonNumber,
                    descending,
                    entry => entry.EpisodeNumber),

                EpisodeSortField.Type => Chain(
                    query,
                    entry => entry.Type,
                    entry => entry.SeasonNumber,
                    descending,
                    entry => entry.EpisodeNumber),

                EpisodeSortField.ReleaseDate => ChainNullsLast(
                    query,
                    entry => entry.ReleaseDate,
                    entry => entry.SeasonNumber,
                    descending,
                    entry => entry.EpisodeNumber),

                EpisodeSortField.Rating => ChainNullsLast(
                    query,
                    entry => entry.Rating,
                    entry => entry.SeasonNumber,
                    descending,
                    entry => entry.EpisodeNumber),

                _ => Chain(
                    query,
                    entry => entry.SeasonNumber,
                    entry => entry.EpisodeNumber,
                    descending)
            };
        }

        private static IOrderedQueryable<EpisodeEntity> Chain<TKey1, TKey2>(
            IQueryable<EpisodeEntity> source,
            Expression<Func<EpisodeEntity, TKey1>> primary,
            Expression<Func<EpisodeEntity, TKey2>> tieBreak,
            bool descending,
            Expression<Func<EpisodeEntity, int>>? secondTieBreak = null)
        {
            var ordered = descending
                ? source.OrderByDescending(primary)
                : source.OrderBy(primary);

            var broken = descending
                ? ordered.ThenByDescending(tieBreak)
                : ordered.ThenBy(tieBreak);

            return secondTieBreak is null
                ? broken
                : descending
                    ? broken.ThenByDescending(secondTieBreak)
                    : broken.ThenBy(secondTieBreak);
        }

        /// <summary>Like <see cref="Chain"/>, except that rows without a value go last.</summary>
        private static IOrderedQueryable<EpisodeEntity> ChainNullsLast<TKey1, TKey2>(
            IQueryable<EpisodeEntity> source,
            Expression<Func<EpisodeEntity, TKey1>> primary,
            Expression<Func<EpisodeEntity, TKey2>> tieBreak,
            bool descending,
            Expression<Func<EpisodeEntity, int>>? secondTieBreak = null)
        {
            var isNull = Expression.Lambda<Func<EpisodeEntity, bool>>(
                Expression.Equal(
                    primary.Body,
                    Expression.Constant(null, primary.Body.Type)),
                primary.Parameters);

            var withGrouped = source.OrderBy(isNull);

            var broken = descending
                ? withGrouped.ThenByDescending(primary).ThenByDescending(tieBreak)
                : withGrouped.ThenBy(primary).ThenBy(tieBreak);

            return secondTieBreak is null
                ? broken
                : descending
                    ? broken.ThenByDescending(secondTieBreak)
                    : broken.ThenBy(secondTieBreak);
        }

        private static EpisodeListItem ToListItem(StoredEpisode row) => new()
        {
            Id = row.Id,
            SeriesId = row.SeriesId,
            SeasonNumber = row.SeasonNumber,
            EpisodeNumber = row.EpisodeNumber,
            Name = row.Name,
            Status = row.Status,
            Type = row.Type,
            Source = row.Source,
            ImageUrl = row.ImageUrl,
            ReleaseDate = row.ReleaseDate,
            Rating = row.Rating
        };

        /// <summary>The columns a page of the table draws, before the shape it returns.</summary>
        private sealed class StoredEpisode
        {
            public Guid Id { get; init; }
            public Guid SeriesId { get; init; }
            public int SeasonNumber { get; init; }
            public int EpisodeNumber { get; init; }
            public string? Name { get; init; }
            public EpisodeStatus Status { get; init; }
            public EpisodeType Type { get; init; }
            public string Source { get; init; } = string.Empty;
            public string? ImageUrl { get; init; }
            public DateOnly? ReleaseDate { get; init; }
            public decimal? Rating { get; init; }
        }
    }
}
