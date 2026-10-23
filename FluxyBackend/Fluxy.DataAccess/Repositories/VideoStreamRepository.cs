using System.Linq.Expressions;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Models;
using Fluxy.Core.Streams.ReadModels;
using Fluxy.Core.Streams.Repositories;
using Fluxy.Core.Streams.Rows;
using Fluxy.Core.Streams.Utils;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IVideoStreamRepository"/>.
    /// </summary>
    /// <remarks>
    /// The two lists are rows of their own. The detail read fetches each with one query, while a
    /// page never asks for them: twenty rows would otherwise cost forty more reads for data the
    /// table does not draw. The update read does load both, because deciding which rows to
    /// remove is a comparison against what the row already holds - and an unloaded collection
    /// reads as empty rather than as "unknown".
    /// </remarks>
    internal sealed class VideoStreamRepository : IVideoStreamRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoStreamRepository"/> class.
        /// </summary>
        /// <param name="context">Context the stream rows and both lists are read from and staged in.</param>
        public VideoStreamRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<VideoStreamPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? categoryId,
            VideoStreamStatus? status,
            VideoStreamType? type,
            VideoStreamSortField sortBy,
            VideoStreamSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            var query = _context.VideoStreams.AsNoTracking();

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
                .Select(entry => new StoredStream
                {
                    Id = entry.Id,
                    Name = entry.Name,
                    Status = entry.Status,
                    Type = entry.Type,
                    CategoryId = entry.CategoryId,
                    CategoryName = entry.Category!.Name,
                    LogoUrl = entry.LogoUrl,
                    ActiveSource = entry.ActiveSource,
                    EpgSourceId = entry.EpgSourceId,
                    EpgChannelKey = entry.EpgChannelKey,
                    SortOrder = entry.SortOrder
                })
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new VideoStreamPage
            {
                Items = rows.Select(ToListItem).ToList(),
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public async Task<VideoStream?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var stream = await _context.VideoStreams
                .AsNoTracking()
                .Include(entry => entry.Category)
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            if (stream is null)
            {
                return null;
            }

            var sources = await ReadSourcesAsync(id, cancellationToken);
            var files = await ReadFilesAsync(id, cancellationToken);

            return stream.ToModel(sources, files);
        }

        /// <inheritdoc />
        public async Task<VideoStream?> FindForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var stream = await _context.VideoStreams
                .Include(entry => entry.Category)
                .Include(entry => entry.StreamSources)
                .Include(entry => entry.StreamFiles)
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            if (stream is null)
            {
                return null;
            }

            var sources = stream.StreamSources
                .OrderBy(entry => entry.Position)
                .Select(entry => entry.Url)
                .ToList();

            var files = stream.StreamFiles
                .OrderBy(entry => entry.Position)
                .Select(entry => entry.Path)
                .ToList();

            return stream.ToModel(sources, files);
        }

        /// <inheritdoc />
        public Task<bool> CategoryIsUsableAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default)
            => _context.ContentCategories
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Id == categoryId && entry.Type == ContentType.VideoStream,
                    cancellationToken);

        /// <inheritdoc />
        public Task<bool> IsNameTakenAsync(
            string name,
            Guid categoryId,
            Guid? exceptStream,
            CancellationToken cancellationToken = default)
            => _context.VideoStreams
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Name == name
                        && entry.CategoryId == categoryId
                        && (exceptStream == null || entry.Id != exceptStream),
                    cancellationToken);

        /// <inheritdoc />
        public async Task<IReadOnlyList<VideoStreamImportRow>> GetImportRowsAsync(
            Guid categoryId,
            IReadOnlyCollection<string> names,
            CancellationToken cancellationToken = default)
        {
            if (names.Count == 0)
            {
                return [];
            }

            var wanted = names.ToArray();

            return await _context.VideoStreams
                .AsNoTracking()
                .Where(entry => entry.CategoryId == categoryId && wanted.Contains(entry.Name))
                .Select(entry => new VideoStreamImportRow
                {
                    Id = entry.Id,
                    Name = entry.Name,
                    Type = entry.Type,
                })
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<int> GetLastSortOrderAsync(
            Guid categoryId,
            Guid? exceptStream,
            CancellationToken cancellationToken = default)
            => await _context.VideoStreams
                .Where(entry => entry.CategoryId == categoryId
                    && (exceptStream == null || entry.Id != exceptStream))
                .MaxAsync(entry => (int?)entry.SortOrder, cancellationToken) ?? 0;

        /// <inheritdoc />
        public Task<Guid> AddAsync(
            VideoStreamWrite stream,
            int sortOrder,
            CancellationToken cancellationToken = default)
        {
            var entity = BuildEntity(stream, sortOrder);
            _context.VideoStreams.Add(entity);

            return Task.FromResult(entity.Id);
        }

        /// <inheritdoc />
        public Task AddRangeAsync(
            IReadOnlyList<VideoStreamWrite> streams,
            int firstSortOrder,
            CancellationToken cancellationToken = default)
        {
            for (var index = 0; index < streams.Count; index++)
            {
                _context.VideoStreams.Add(BuildEntity(streams[index], firstSortOrder + index));
            }

            return Task.CompletedTask;
        }

        /// <summary>Builds one tracked row, with both lists attached, at the position it must take.</summary>
        private static VideoStreamEntity BuildEntity(VideoStreamWrite stream, int sortOrder)
        {
            var entity = new VideoStreamEntity
            {
                Name = stream.Name,
                Status = stream.Status,
                Type = stream.Type,
                CategoryId = stream.CategoryId,
                LogoUrl = stream.LogoUrl,
                ActiveSource = null,
                Enigma2Sid = stream.Enigma2Sid,
                DelayMinutes = stream.DelayMinutes,
                SeriesId = stream.SeriesId,
                VodId = stream.VodId,
                EpgSourceId = stream.EpgSourceId,
                EpgChannelKey = stream.EpgChannelKey,
                ArchiveDays = stream.ArchiveDays,
                SortOrder = sortOrder,
                RestreamUserAgent = stream.RestreamUserAgent,
                RestreamProxy = stream.RestreamProxy,
                RestreamCookies = stream.RestreamCookies,
                RestreamHeaders = stream.RestreamHeaders
            };

            AppendSources(entity, stream.Sources);
            AppendFiles(entity, stream.Files);

            return entity;
        }

        /// <inheritdoc />
        public async Task<bool> UpdateAsync(
            Guid id,
            VideoStreamWrite stream,
            int? sortOrder,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindTrackedWithListsAsync(id, cancellationToken);

            if (entity is null)
            {
                return false;
            }

            entity.Name = stream.Name;
            entity.Type = stream.Type;
            entity.Status = stream.Status;
            entity.CategoryId = stream.CategoryId;
            entity.LogoUrl = stream.LogoUrl;
            entity.Enigma2Sid = stream.Enigma2Sid;
            entity.DelayMinutes = stream.DelayMinutes;
            entity.SeriesId = stream.SeriesId;
            entity.VodId = stream.VodId;
            entity.EpgSourceId = stream.EpgSourceId;
            entity.EpgChannelKey = stream.EpgChannelKey;
            entity.ArchiveDays = stream.ArchiveDays;
            entity.RestreamUserAgent = stream.RestreamUserAgent;
            entity.RestreamProxy = stream.RestreamProxy;
            entity.RestreamCookies = stream.RestreamCookies;
            entity.RestreamHeaders = stream.RestreamHeaders;

            if (sortOrder is { } position)
            {
                entity.SortOrder = position;
            }

            ReplaceSources(entity, stream.Sources);
            ReplaceFiles(entity, stream.Files);

            return true;
        }

        /// <inheritdoc />
        public async Task UpdateRangeAsync(
            IReadOnlyList<VideoStreamBatchUpdate> updates,
            CancellationToken cancellationToken = default)
        {
            if (updates.Count == 0)
            {
                return;
            }

            var ids = updates.Select(update => update.Id).ToArray();

            var entities = await _context.VideoStreams
                .Where(entry => ids.Contains(entry.Id))
                .Include(entry => entry.StreamSources)
                .Include(entry => entry.StreamFiles)
                .ToDictionaryAsync(entry => entry.Id, cancellationToken);

            foreach (var update in updates)
            {
                if (!entities.TryGetValue(update.Id, out var entity))
                {
                    continue;
                }

                if (update.OverwriteSettings)
                {
                    entity.Type = update.Type;
                    entity.Status = update.Status;
                    entity.DelayMinutes = update.DelayMinutes;
                    entity.ArchiveDays = update.ArchiveDays;
                    entity.RestreamUserAgent = update.RestreamUserAgent;
                    entity.RestreamProxy = update.RestreamProxy;
                    entity.RestreamCookies = update.RestreamCookies;
                    entity.RestreamHeaders = update.RestreamHeaders;

                    // A kind that does not relay from a source list keeps neither a series, a
                    // video-playlist item, nor a stored file list - those payloads belong to
                    // other kinds. A URL kind keeps its existing sources unless the import
                    // appends to them below.
                    entity.SeriesId = null;
                    entity.VodId = null;
                    ReplaceFiles(entity, []);

                    // A converted stream that has no source yet takes the playlist's source,
                    // so it can not end up as a URL kind with nothing to relay.
                    if (IsUrlKind(update.Type) && entity.StreamSources.Count == 0 &&
                        !string.IsNullOrWhiteSpace(update.SourceToAppend))
                    {
                        entity.StreamSources.Add(new VideoStreamSourceEntity
                        {
                            StreamId = entity.Id,
                            Position = 0,
                            Url = update.SourceToAppend
                        });
                    }
                }

                if (update.AppendSource && IsUrlKind(entity.Type) &&
                    !string.IsNullOrWhiteSpace(update.SourceToAppend))
                {
                    var stored = entity.StreamSources
                        .OrderBy(source => source.Position)
                        .Select(source => source.Url)
                        .ToList();

                    if (!stored.Contains(update.SourceToAppend, StringComparer.Ordinal))
                    {
                        stored.Add(update.SourceToAppend);
                        ReplaceSources(entity, stored);
                    }
                }
            }
        }

        /// <summary>Whether a stream kind relays from a list of source addresses.</summary>
        private static bool IsUrlKind(VideoStreamType type)
            => type is VideoStreamType.Permanent or VideoStreamType.OnDemand
                or VideoStreamType.Redirect;

        /// <inheritdoc />
        public async Task<bool> RemoveAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.VideoStreams.FindAsync([id], cancellationToken);

            if (entity is null)
            {
                return false;
            }

            _context.VideoStreams.Remove(entity);
            return true;
        }

        /// <inheritdoc />
        public async Task<VideoStreamStatus?> SetStatusAsync(
            Guid id,
            VideoStreamStatus status,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.VideoStreams.FindAsync([id], cancellationToken);

            if (entity is null)
            {
                return null;
            }

            var previous = entity.Status;
            entity.Status = status;
            return previous;
        }

        /// <summary>Reads the stored source list, in the order the relay uses it.</summary>
        private async Task<IReadOnlyList<string>> ReadSourcesAsync(
            Guid id,
            CancellationToken cancellationToken)
            => await _context.VideoStreamSources
                .AsNoTracking()
                .Where(entry => entry.StreamId == id)
                .OrderBy(entry => entry.Position)
                .Select(entry => entry.Url)
                .ToListAsync(cancellationToken);

        /// <summary>Reads the stored file list, in the order it is played.</summary>
        private async Task<IReadOnlyList<string>> ReadFilesAsync(
            Guid id,
            CancellationToken cancellationToken)
            => await _context.VideoStreamFiles
                .AsNoTracking()
                .Where(entry => entry.StreamId == id)
                .OrderBy(entry => entry.Position)
                .Select(entry => entry.Path)
                .ToListAsync(cancellationToken);

        private async Task<VideoStreamEntity?> FindTrackedWithListsAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            var entity = await _context.VideoStreams.FindAsync([id], cancellationToken);

            if (entity is null)
            {
                return null;
            }

            var entry = _context.Entry(entity);
            if (!entry.Collection(stream => stream.StreamSources).IsLoaded)
            {
                await entry.Collection(stream => stream.StreamSources)
                    .LoadAsync(cancellationToken);
            }

            if (!entry.Collection(stream => stream.StreamFiles).IsLoaded)
            {
                await entry.Collection(stream => stream.StreamFiles)
                    .LoadAsync(cancellationToken);
            }

            return entity;
        }

        private static void AppendSources(
            VideoStreamEntity entity,
            IReadOnlyList<string> sources)
        {
            var position = 0;
            foreach (var entry in sources)
            {
                entity.StreamSources.Add(new VideoStreamSourceEntity
                {
                    StreamId = entity.Id,
                    Position = position++,
                    Url = entry
                });
            }
        }

        private static void AppendFiles(
            VideoStreamEntity entity,
            IReadOnlyList<string> files)
        {
            var position = 0;
            foreach (var entry in files)
            {
                entity.StreamFiles.Add(new VideoStreamFileEntity
                {
                    StreamId = entity.Id,
                    Position = position++,
                    Path = entry
                });
            }
        }

        /// <summary>Replaces the stored source list with the one the change is sending.</summary>
        private void ReplaceSources(
            VideoStreamEntity stream,
            IReadOnlyList<string> sources)
        {
            var stored = stream.StreamSources
                .OrderBy(entry => entry.Position)
                .ToList();

            if (stored.Select(entry => entry.Url).SequenceEqual(sources, StringComparer.Ordinal))
            {
                return;
            }

            _context.VideoStreamSources.RemoveRange(stored);

            for (var index = 0; index < sources.Count; index++)
            {
                stream.StreamSources.Add(new VideoStreamSourceEntity
                {
                    StreamId = stream.Id,
                    Position = index,
                    Url = sources[index]
                });
            }
        }

        /// <summary>Replaces the stored file list with the one the change is sending.</summary>
        private void ReplaceFiles(
            VideoStreamEntity stream,
            IReadOnlyList<string> files)
        {
            var stored = stream.StreamFiles
                .OrderBy(entry => entry.Position)
                .ToList();

            if (stored.Select(entry => entry.Path).SequenceEqual(files, StringComparer.Ordinal))
            {
                return;
            }

            _context.VideoStreamFiles.RemoveRange(stored);

            for (var index = 0; index < files.Count; index++)
            {
                stream.StreamFiles.Add(new VideoStreamFileEntity
                {
                    StreamId = stream.Id,
                    Position = index,
                    Path = files[index]
                });
            }
        }

        private static IQueryable<VideoStreamEntity> Order(
            IQueryable<VideoStreamEntity> query,
            VideoStreamSortField field,
            VideoStreamSortOrder order)
        {
            var descending = order is VideoStreamSortOrder.Descending;

            return field switch
            {
                VideoStreamSortField.Category => Chain(
                    query, entry => entry.Category!.Name, entry => entry.Name, descending),

                VideoStreamSortField.Status => Chain(
                    query, entry => entry.Status, entry => entry.Name, descending),

                VideoStreamSortField.Type => Chain(
                    query, entry => entry.Type, entry => entry.Name, descending),

                VideoStreamSortField.SortOrder => Chain(
                    query, entry => entry.SortOrder, entry => entry.Name, descending),

                _ => Chain(query, entry => entry.Name, entry => entry.Id, descending)
            };
        }

        private static IOrderedQueryable<VideoStreamEntity> Chain<TKey1, TKey2>(
            IQueryable<VideoStreamEntity> source,
            Expression<Func<VideoStreamEntity, TKey1>> primary,
            Expression<Func<VideoStreamEntity, TKey2>> tieBreak,
            bool descending)
        {
            var ordered = descending
                ? source.OrderByDescending(primary)
                : source.OrderBy(primary);

            return descending
                ? ordered.ThenByDescending(tieBreak)
                : ordered.ThenBy(tieBreak);
        }

        private static VideoStreamListItem ToListItem(StoredStream row) => new()
        {
            Id = row.Id,
            Name = row.Name,
            Status = row.Status,
            Type = row.Type,
            CategoryId = row.CategoryId,
            CategoryName = row.CategoryName,
            LogoUrl = row.LogoUrl,
            ActiveSource = row.ActiveSource,
            HasEpg = VideoStreamEpg.Configured(row.EpgSourceId, row.EpgChannelKey),
            SortOrder = row.SortOrder
        };

        /// <summary>The columns a page of the streams table draws, before the shape it returns.</summary>
        private sealed class StoredStream
        {
            public Guid Id { get; init; }
            public string Name { get; init; } = string.Empty;
            public VideoStreamStatus Status { get; init; }
            public VideoStreamType Type { get; init; }
            public Guid CategoryId { get; init; }
            public string CategoryName { get; init; } = string.Empty;
            public string? LogoUrl { get; init; }
            public string? ActiveSource { get; init; }
            public Guid? EpgSourceId { get; init; }
            public string? EpgChannelKey { get; init; }
            public int SortOrder { get; init; }
        }
    }
}
