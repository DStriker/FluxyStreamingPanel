using System.Linq.Expressions;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Models;
using Fluxy.Core.Streams.ReadModels;
using Fluxy.Core.Streams.Repositories;
using Fluxy.Core.Streams.Rows;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IAudioStreamRepository"/>.
    /// </summary>
    /// <remarks>
    /// The source list is rows of its own. The detail read fetches it with one query, while a
    /// page never asks for it: twenty rows would otherwise cost twenty more reads for data the
    /// table does not draw. The update read does load it, because deciding which rows to remove
    /// is a comparison against what the row already holds - and an unloaded collection reads as
    /// empty rather than as "unknown".
    /// </remarks>
    internal sealed class AudioStreamRepository : IAudioStreamRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioStreamRepository"/> class.
        /// </summary>
        /// <param name="context">Context the stream rows and their sources are read from and staged in.</param>
        public AudioStreamRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<AudioStreamPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? categoryId,
            AudioStreamStatus? status,
            AudioStreamType? type,
            AudioStreamSortField sortBy,
            AudioStreamSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            var query = _context.AudioStreams.AsNoTracking();

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
                    ActiveSource = entry.ActiveSource
                })
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new AudioStreamPage
            {
                Items = rows.Select(ToListItem).ToList(),
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public async Task<AudioStream?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var stream = await _context.AudioStreams
                .AsNoTracking()
                .Include(entry => entry.Category)
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            if (stream is null)
            {
                return null;
            }

            var sources = await ReadSourcesAsync(id, cancellationToken);

            return stream.ToModel(sources);
        }

        /// <inheritdoc />
        public async Task<AudioStream?> FindForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var stream = await _context.AudioStreams
                .Include(entry => entry.Category)
                .Include(entry => entry.StreamSources)
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            if (stream is null)
            {
                return null;
            }

            var sources = stream.StreamSources
                .OrderBy(entry => entry.Position)
                .Select(entry => entry.Url)
                .ToList();

            return stream.ToModel(sources);
        }

        /// <inheritdoc />
        public Task<bool> CategoryIsUsableAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default)
            => _context.ContentCategories
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Id == categoryId && entry.Type == ContentType.AudioStream,
                    cancellationToken);

        /// <inheritdoc />
        public Task<bool> IsNameTakenAsync(
            string name,
            Guid categoryId,
            Guid? exceptStream,
            CancellationToken cancellationToken = default)
            => _context.AudioStreams
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Name == name
                        && entry.CategoryId == categoryId
                        && (exceptStream == null || entry.Id != exceptStream),
                    cancellationToken);

        /// <inheritdoc />
        public async Task<IReadOnlyList<AudioStreamImportRow>> GetImportRowsAsync(
            Guid categoryId,
            IReadOnlyCollection<string> names,
            CancellationToken cancellationToken = default)
        {
            if (names.Count == 0)
            {
                return [];
            }

            var wanted = names.ToArray();

            return await _context.AudioStreams
                .AsNoTracking()
                .Where(entry => entry.CategoryId == categoryId && wanted.Contains(entry.Name))
                .Select(entry => new AudioStreamImportRow
                {
                    Id = entry.Id,
                    Name = entry.Name,
                    Type = entry.Type
                })
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<Guid> AddAsync(
            AudioStreamWrite stream,
            CancellationToken cancellationToken = default)
        {
            var entity = BuildEntity(stream);
            _context.AudioStreams.Add(entity);

            return Task.FromResult(entity.Id);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<Guid>> AddRangeAsync(
            IReadOnlyList<AudioStreamWrite> streams,
            CancellationToken cancellationToken = default)
        {
            var created = new List<Guid>(streams.Count);

            foreach (var stream in streams)
            {
                var entity = BuildEntity(stream);

                _context.AudioStreams.Add(entity);
                created.Add(entity.Id);
            }

            return Task.FromResult<IReadOnlyList<Guid>>(created);
        }

        /// <summary>Builds one tracked row, with its sources attached.</summary>
        private static AudioStreamEntity BuildEntity(AudioStreamWrite stream)
        {
            var entity = new AudioStreamEntity
            {
                Name = stream.Name,
                Status = stream.Status,
                Type = stream.Type,
                CategoryId = stream.CategoryId,
                LogoUrl = stream.LogoUrl,
                ActiveSource = null,
                Enigma2Sid = stream.Enigma2Sid,
                RestreamUserAgent = stream.RestreamUserAgent,
                RestreamProxy = stream.RestreamProxy,
                RestreamCookies = stream.RestreamCookies,
                RestreamHeaders = stream.RestreamHeaders
            };

            AppendSources(entity, stream.Sources);

            return entity;
        }

        /// <inheritdoc />
        public async Task<bool> UpdateAsync(
            Guid id,
            AudioStreamWrite stream,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindTrackedWithSourcesAsync(id, cancellationToken);

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
            entity.RestreamUserAgent = stream.RestreamUserAgent;
            entity.RestreamProxy = stream.RestreamProxy;
            entity.RestreamCookies = stream.RestreamCookies;
            entity.RestreamHeaders = stream.RestreamHeaders;

            ReplaceSources(entity, stream.Sources);

            return true;
        }

        /// <inheritdoc />
        public async Task UpdateRangeAsync(
            IReadOnlyList<AudioStreamBatchUpdate> updates,
            CancellationToken cancellationToken = default)
        {
            if (updates.Count == 0)
            {
                return;
            }

            var ids = updates.Select(update => update.Id).ToArray();

            var entities = await _context.AudioStreams
                .Where(entry => ids.Contains(entry.Id))
                .Include(entry => entry.StreamSources)
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
                    entity.RestreamUserAgent = update.RestreamUserAgent;
                    entity.RestreamProxy = update.RestreamProxy;
                    entity.RestreamCookies = update.RestreamCookies;
                    entity.RestreamHeaders = update.RestreamHeaders;

                    // A converted stream that has no source yet takes the playlist's source,
                    // so it cannot end up as a kind with nothing to relay.
                    if (entity.StreamSources.Count == 0 &&
                        !string.IsNullOrWhiteSpace(update.SourceToAppend))
                    {
                        entity.StreamSources.Add(new AudioStreamSourceEntity
                        {
                            StreamId = entity.Id,
                            Position = 0,
                            Url = update.SourceToAppend
                        });
                    }
                }

                if (update.AppendSource && !string.IsNullOrWhiteSpace(update.SourceToAppend))
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

        /// <inheritdoc />
        public async Task<bool> RemoveAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.AudioStreams.FindAsync([id], cancellationToken);

            if (entity is null)
            {
                return false;
            }

            _context.AudioStreams.Remove(entity);
            return true;
        }

        /// <inheritdoc />
        public async Task<AudioStreamStatus?> SetStatusAsync(
            Guid id,
            AudioStreamStatus status,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.AudioStreams.FindAsync([id], cancellationToken);

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
            => await _context.AudioStreamSources
                .AsNoTracking()
                .Where(entry => entry.StreamId == id)
                .OrderBy(entry => entry.Position)
                .Select(entry => entry.Url)
                .ToListAsync(cancellationToken);

        private async Task<AudioStreamEntity?> FindTrackedWithSourcesAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            var entity = await _context.AudioStreams.FindAsync([id], cancellationToken);

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

            return entity;
        }

        private static void AppendSources(
            AudioStreamEntity entity,
            IReadOnlyList<string> sources)
        {
            var position = 0;
            foreach (var entry in sources)
            {
                entity.StreamSources.Add(new AudioStreamSourceEntity
                {
                    StreamId = entity.Id,
                    Position = position++,
                    Url = entry
                });
            }
        }

        /// <summary>Replaces the stored source list with the one the change is sending.</summary>
        private void ReplaceSources(
            AudioStreamEntity stream,
            IReadOnlyList<string> sources)
        {
            var stored = stream.StreamSources
                .OrderBy(entry => entry.Position)
                .ToList();

            if (stored.Select(entry => entry.Url).SequenceEqual(sources, StringComparer.Ordinal))
            {
                return;
            }

            _context.AudioStreamSources.RemoveRange(stored);

            for (var index = 0; index < sources.Count; index++)
            {
                stream.StreamSources.Add(new AudioStreamSourceEntity
                {
                    StreamId = stream.Id,
                    Position = index,
                    Url = sources[index]
                });
            }
        }

        private static IQueryable<AudioStreamEntity> Order(
            IQueryable<AudioStreamEntity> query,
            AudioStreamSortField field,
            AudioStreamSortOrder order)
        {
            var descending = order is AudioStreamSortOrder.Descending;

            return field switch
            {
                AudioStreamSortField.Category => Chain(
                    query, entry => entry.Category!.Name, entry => entry.Name, descending),

                AudioStreamSortField.Status => Chain(
                    query, entry => entry.Status, entry => entry.Name, descending),

                AudioStreamSortField.Type => Chain(
                    query, entry => entry.Type, entry => entry.Name, descending),

                _ => Chain(query, entry => entry.Name, entry => entry.Id, descending)
            };
        }

        private static IOrderedQueryable<AudioStreamEntity> Chain<TKey1, TKey2>(
            IQueryable<AudioStreamEntity> source,
            Expression<Func<AudioStreamEntity, TKey1>> primary,
            Expression<Func<AudioStreamEntity, TKey2>> tieBreak,
            bool descending)
        {
            var ordered = descending
                ? source.OrderByDescending(primary)
                : source.OrderBy(primary);

            return descending
                ? ordered.ThenByDescending(tieBreak)
                : ordered.ThenBy(tieBreak);
        }

        private static AudioStreamListItem ToListItem(StoredStream row) => new()
        {
            Id = row.Id,
            Name = row.Name,
            Status = row.Status,
            Type = row.Type,
            CategoryId = row.CategoryId,
            CategoryName = row.CategoryName,
            LogoUrl = row.LogoUrl,
            ActiveSource = row.ActiveSource
        };

        /// <summary>The columns a page of the streams table draws, before the shape it returns.</summary>
        private sealed class StoredStream
        {
            public Guid Id { get; init; }
            public string Name { get; init; } = string.Empty;
            public AudioStreamStatus Status { get; init; }
            public AudioStreamType Type { get; init; }
            public Guid CategoryId { get; init; }
            public string CategoryName { get; init; } = string.Empty;
            public string? LogoUrl { get; init; }
            public string? ActiveSource { get; init; }
        }
    }
}
