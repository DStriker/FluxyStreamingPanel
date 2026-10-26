using System.Linq.Expressions;
using Fluxy.Core.Epg.Commands;
using Fluxy.Core.Epg.Enums;
using Fluxy.Core.Epg.Models;
using Fluxy.Core.Epg.ReadModels;
using Fluxy.Core.Epg.Repositories;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IEpgSourceRepository"/>.
    /// </summary>
    /// <remarks>
    /// Every read projects the columns it returns and leaves the raw guide in the table: a
    /// page of twenty sources, and even the edit form of one, has no use for a file that can be
    /// megabytes. The channels read queries the streams table, the same way the category
    /// repository counts content - the question is asked by this resource about its own rows.
    /// </remarks>
    internal sealed class EpgSourceRepository : IEpgSourceRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="EpgSourceRepository"/> class.
        /// </summary>
        /// <param name="context">Context the source rows are read from and staged in.</param>
        public EpgSourceRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<EpgSourcePage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            EpgSourceSortField sortBy,
            EpgSourceSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            var query = _context.EpgSources.AsNoTracking();

            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                query = query.Where(source =>
                    source.Name.ToLower().Contains(needle)
                    || source.Url.ToLower().Contains(needle));
            }

            var total = await query.CountAsync(cancellationToken);

            query = Order(query, sortBy, sortOrder);

            var rows = await query
                .Select(source => new EpgSourceView
                {
                    Id = source.Id,
                    Name = source.Name,
                    Url = source.Url,
                    DaysKeep = source.DaysKeep,
                    LastEpgUpdatedAt = source.LastEpgUpdatedAt
                })
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new EpgSourcePage
            {
                Items = rows,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public Task<EpgSource?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => _context.EpgSources
                .AsNoTracking()
                .Where(source => source.Id == id)
                .Select(source => new EpgSource
                {
                    Id = source.Id,
                    Name = source.Name,
                    Url = source.Url,
                    DaysKeep = source.DaysKeep,
                    LastEpgUpdatedAt = source.LastEpgUpdatedAt,

                    // Deliberately not selected. The raw guide is what the updater parses, and
                    // no endpoint in this pass reads it - loading a file of megabytes to answer
                    // an edit form would be the one place this repository costs more than the
                    // row it describes.
                    RawData = null
                })
                .FirstOrDefaultAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<EpgSourceChannelPage> GetChannelsPageAsync(
            Guid sourceId,
            int page,
            int pageSize,
            string? search,
            EpgSourceChannelSortField sortBy,
            EpgSourceChannelSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            var query = _context.VideoStreams
                .AsNoTracking()
                .Where(stream => stream.EpgSourceId == sourceId);

            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                query = query.Where(stream =>
                    stream.Name.ToLower().Contains(needle)
                    || (stream.EpgChannelKey ?? string.Empty).ToLower().Contains(needle));
            }

            var total = await query.CountAsync(cancellationToken);

            query = OrderChannels(query, sortBy, sortOrder);

            var rows = await query
                .Select(stream => new EpgSourceChannel
                {
                    StreamId = stream.Id,
                    StreamName = stream.Name,

                    // The pair rule makes a null key impossible on a row that names a source,
                    // but a key planted by a fixture or by an older write must still draw as an
                    // empty cell rather than break the page.
                    EpgChannelKey = stream.EpgChannelKey ?? string.Empty
                })
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new EpgSourceChannelPage
            {
                Items = rows,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public Task<bool> IsNameTakenAsync(
            string name,
            Guid? exceptSource,
            CancellationToken cancellationToken = default)
            => _context.EpgSources
                .AsNoTracking()
                .AnyAsync(
                    source => source.Name == name
                        && (exceptSource == null || source.Id != exceptSource),
                    cancellationToken);

        /// <inheritdoc />
        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.EpgSources
                .AsNoTracking()
                .AnyAsync(source => source.Id == id, cancellationToken);

        /// <inheritdoc />
        public Task<int> CountStreamsUsingAsync(
            Guid sourceId,
            CancellationToken cancellationToken = default)
            => _context.VideoStreams
                .AsNoTracking()
                .CountAsync(stream => stream.EpgSourceId == sourceId, cancellationToken);

        /// <inheritdoc />
        public Task<Guid> AddAsync(
            EpgSourceWrite source,
            CancellationToken cancellationToken = default)
        {
            var entity = new EpgSourceEntity
            {
                Name = source.Name,
                Url = source.Url,
                DaysKeep = source.DaysKeep
            };

            _context.EpgSources.Add(entity);

            return Task.FromResult(entity.Id);
        }

        /// <inheritdoc />
        public async Task<bool> UpdateAsync(
            Guid id,
            EpgSourceWrite source,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.EpgSources.FindAsync([id], cancellationToken);

            if (entity is null)
            {
                return false;
            }

            // The raw guide and the moment of the last refresh are left alone on purpose: they
            // are the updater's columns, and a name change is not a refresh.
            entity.Name = source.Name;
            entity.Url = source.Url;
            entity.DaysKeep = source.DaysKeep;

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> RemoveAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.EpgSources.FindAsync([id], cancellationToken);

            if (entity is null)
            {
                return false;
            }

            _context.EpgSources.Remove(entity);
            return true;
        }

        private static IQueryable<EpgSourceEntity> Order(
            IQueryable<EpgSourceEntity> query,
            EpgSourceSortField field,
            EpgSourceSortOrder order)
        {
            var descending = order is EpgSourceSortOrder.Descending;

            return field switch
            {
                EpgSourceSortField.Url => Chain(
                    query, source => source.Url, source => source.Name, descending),

                EpgSourceSortField.DaysKeep => Chain(
                    query, source => source.DaysKeep, source => source.Name, descending),

                // Sources that were never refreshed group last in either direction: a page
                // ordered by "last refreshed" must not open with the ones that never were.
                EpgSourceSortField.LastEpgUpdatedAt => ChainNullsLast(
                    query, source => source.LastEpgUpdatedAt, source => source.Name, descending),

                _ => Chain(query, source => source.Name, source => source.Id, descending)
            };
        }

        private static IQueryable<VideoStreamEntity> OrderChannels(
            IQueryable<VideoStreamEntity> query,
            EpgSourceChannelSortField field,
            EpgSourceChannelSortOrder order)
        {
            var descending = order is EpgSourceChannelSortOrder.Descending;

            return field switch
            {
                EpgSourceChannelSortField.ChannelKey => Chain(
                    query,
                    stream => stream.EpgChannelKey ?? string.Empty,
                    stream => stream.Name,
                    descending),

                _ => Chain(
                    query, stream => stream.Name, stream => stream.Id, descending)
            };
        }

        private static IOrderedQueryable<TEntity> Chain<TEntity, TKey1, TKey2>(
            IQueryable<TEntity> source,
            Expression<Func<TEntity, TKey1>> primary,
            Expression<Func<TEntity, TKey2>> tieBreak,
            bool descending)
        {
            var ordered = descending
                ? source.OrderByDescending(primary)
                : source.OrderBy(primary);

            return descending
                ? ordered.ThenByDescending(tieBreak)
                : ordered.ThenBy(tieBreak);
        }

        /// <summary>Like <see cref="Chain"/> of this type, except that rows without a value go last.</summary>
        private static IOrderedQueryable<TEntity> ChainNullsLast<TEntity, TKey1, TKey2>(
            IQueryable<TEntity> source,
            Expression<Func<TEntity, TKey1>> primary,
            Expression<Func<TEntity, TKey2>> tieBreak,
            bool descending)
        {
            var isNull = Expression.Lambda<Func<TEntity, bool>>(
                Expression.Equal(
                    primary.Body,
                    Expression.Constant(null, primary.Body.Type)),
                primary.Parameters);

            var withGrouped = source.OrderBy(isNull);

            return descending
                ? withGrouped.ThenByDescending(primary).ThenByDescending(tieBreak)
                : withGrouped.ThenBy(primary).ThenBy(tieBreak);
        }
    }
}
