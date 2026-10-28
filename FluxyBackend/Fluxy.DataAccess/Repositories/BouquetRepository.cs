using Fluxy.Core.Content.Commands;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Models;
using Fluxy.Core.Content.ReadModels;
using Fluxy.Core.Content.Repositories;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IBouquetRepository"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The list read keeps the order the index was designed for - position first, with creation
    /// time and identifier as tie-breaks - for the reason
    /// <see cref="ContentCategoryRepository"/> gives: a deletion leaves a gap and a raced create
    /// can share a position, so without the tie-breaks the database is free to answer a
    /// different order on every read.
    /// </para>
    /// <para>
    /// The four content kinds are each handled beside their own table. They are four tables
    /// rather than one polymorphic one - which is what buys the foreign keys that delete a
    /// membership together with the content it points at - and every public member of this
    /// class dispatches on <see cref="ContentType"/> once, so the service keeps asking its
    /// questions by kind and never by table.
    /// </para>
    /// </remarks>
    internal sealed class BouquetRepository : IBouquetRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="BouquetRepository"/> class.
        /// </summary>
        /// <param name="context">Context the bouquet rows are read from and staged in.</param>
        public BouquetRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<BouquetListItem>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            var bouquets = await _context.Bouquets
                .AsNoTracking()
                .OrderBy(entry => entry.SortOrder)
                .ThenBy(entry => entry.CreatedAt)
                .ThenBy(entry => entry.Id)
                .Select(entry => new { entry.Id, entry.Name, entry.SortOrder })
                .ToListAsync(cancellationToken);

            if (bouquets.Count == 0)
            {
                return [];
            }

            // Four grouped counts rather than eight correlated subqueries: each one is a range
            // scan grouped by the bouquet identifier, and the answers are stitched together in
            // memory where the list is one screenful by construction.
            var videoCounts = await CountRowsAsync(_context.BouquetVideoStreams, cancellationToken);
            var audioCounts = await CountRowsAsync(_context.BouquetAudioStreams, cancellationToken);
            var vodCounts = await CountRowsAsync(_context.BouquetVodItems, cancellationToken);
            var seriesCounts = await CountRowsAsync(_context.BouquetSeries, cancellationToken);

            return bouquets
                .Select(bouquet => new BouquetListItem
                {
                    Id = bouquet.Id,
                    Name = bouquet.Name,
                    SortOrder = bouquet.SortOrder,
                    VideoStreamCount = videoCounts.GetValueOrDefault(bouquet.Id),
                    AudioStreamCount = audioCounts.GetValueOrDefault(bouquet.Id),
                    VideoOnDemandCount = vodCounts.GetValueOrDefault(bouquet.Id),
                    SeriesCount = seriesCounts.GetValueOrDefault(bouquet.Id)
                })
                .ToList();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Bouquet>> GetTrackedAsync(
            CancellationToken cancellationToken = default)
        {
            var rows = await _context.Bouquets
                .OrderBy(entry => entry.SortOrder)
                .ThenBy(entry => entry.CreatedAt)
                .ThenBy(entry => entry.Id)
                .ToListAsync(cancellationToken);

            return rows.Select(row => row.ToModel()).ToList();
        }

        /// <inheritdoc />
        public async Task<Bouquet?> FindForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Bouquets
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            return entity?.ToModel();
        }

        /// <inheritdoc />
        public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
            => _context.Bouquets.AsNoTracking().AnyAsync(cancellationToken);

        /// <inheritdoc />
        public Task<bool> IsNameTakenAsync(
            string name,
            Guid? exceptBouquet,
            CancellationToken cancellationToken = default)
        {
            // Compared in lower case on both sides, because the rule is "one name however it is
            // spelled". This is the same comparison the unique index over lower(name) performs,
            // so the probe and the constraint agree about what a duplicate is; the index remains
            // the authority when two operators race.
            var wanted = name.ToLowerInvariant();

            return _context.Bouquets
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Name.ToLower() == wanted
                        && (exceptBouquet == null || entry.Id != exceptBouquet),
                    cancellationToken);
        }

        /// <inheritdoc />
        public async Task<int> GetLastSortOrderAsync(CancellationToken cancellationToken = default)
            => await _context.Bouquets
                .MaxAsync(entry => (int?)entry.SortOrder, cancellationToken) ?? 0;

        /// <inheritdoc />
        public Task<Guid> AddAsync(
            NewBouquet bouquet,
            int sortOrder,
            CancellationToken cancellationToken = default)
        {
            var entity = new BouquetEntity
            {
                Name = bouquet.Name,
                SortOrder = sortOrder
            };

            _context.Bouquets.Add(entity);
            return Task.FromResult(entity.Id);
        }

        /// <inheritdoc />
        public async Task SetNameAsync(
            Guid id,
            string name,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Bouquets.FindAsync([id], cancellationToken);
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
            var entity = await _context.Bouquets.FindAsync([id], cancellationToken);
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
            var entity = await _context.Bouquets.FindAsync([id], cancellationToken);
            if (entity is not null)
            {
                _context.Bouquets.Remove(entity);
            }
        }

        /// <inheritdoc />
        public async Task<int> CountItemsAsync(
            Guid bouquetId,
            CancellationToken cancellationToken = default)
        {
            // Every kind, for the reason the categories' count gives: the question is "may this
            // bouquet go?", and a bouquet holding one series is just as full as one holding a
            // thousand streams. Separate counts because the four membership tables share no
            // column beyond the bouquet identifier, and each one is an index range scan.
            var videos = await _context.BouquetVideoStreams
                .AsNoTracking()
                .CountAsync(entry => entry.BouquetId == bouquetId, cancellationToken);

            var audio = await _context.BouquetAudioStreams
                .AsNoTracking()
                .CountAsync(entry => entry.BouquetId == bouquetId, cancellationToken);

            var onDemand = await _context.BouquetVodItems
                .AsNoTracking()
                .CountAsync(entry => entry.BouquetId == bouquetId, cancellationToken);

            var series = await _context.BouquetSeries
                .AsNoTracking()
                .CountAsync(entry => entry.BouquetId == bouquetId, cancellationToken);

            return videos + audio + onDemand + series;
        }

        /// <inheritdoc />
        public async Task<BouquetContent> GetContentAsync(
            Guid bouquetId,
            ContentType type,
            CancellationToken cancellationToken = default)
            => type switch
            {
                ContentType.VideoStream => new BouquetContent
                {
                    Members = await ReadVideoMembersAsync(bouquetId, cancellationToken),
                    Available = await ReadVideoAvailableAsync(bouquetId, cancellationToken)
                },
                ContentType.AudioStream => new BouquetContent
                {
                    Members = await ReadAudioMembersAsync(bouquetId, cancellationToken),
                    Available = await ReadAudioAvailableAsync(bouquetId, cancellationToken)
                },
                ContentType.VideoOnDemand => new BouquetContent
                {
                    Members = await ReadVodMembersAsync(bouquetId, cancellationToken),
                    Available = await ReadVodAvailableAsync(bouquetId, cancellationToken)
                },
                ContentType.Series => new BouquetContent
                {
                    Members = await ReadSeriesMembersAsync(bouquetId, cancellationToken),
                    Available = await ReadSeriesAvailableAsync(bouquetId, cancellationToken)
                },
                _ => throw Unsupported(type)
            };

        /// <inheritdoc />
        public Task SetContentAsync(
            Guid bouquetId,
            ContentType type,
            IReadOnlyList<Guid> contentIds,
            CancellationToken cancellationToken = default)
            => type switch
            {
                ContentType.VideoStream => SetVideoContentAsync(bouquetId, contentIds, cancellationToken),
                ContentType.AudioStream => SetAudioContentAsync(bouquetId, contentIds, cancellationToken),
                ContentType.VideoOnDemand => SetVodContentAsync(bouquetId, contentIds, cancellationToken),
                ContentType.Series => SetSeriesContentAsync(bouquetId, contentIds, cancellationToken),
                _ => throw Unsupported(type)
            };

        /// <inheritdoc />
        public async Task<IReadOnlyList<Guid>> GetItemBouquetIdsAsync(
            ContentType type,
            Guid contentId,
            CancellationToken cancellationToken = default)
        {
            // Ordered by the bouquets' own position rather than by the membership rows, so the
            // picker on a content form opens with the same packages in the same sequence every
            // time. The kind decides which membership table answers.
            var bouquets = _context.Bouquets.AsNoTracking();

            return type switch
            {
                ContentType.VideoStream => await _context.BouquetVideoStreams
                    .AsNoTracking()
                    .Where(row => row.VideoStreamId == contentId)
                    .Join(bouquets, row => row.BouquetId, bouquet => bouquet.Id, (_, bouquet) => bouquet)
                    .OrderBy(bouquet => bouquet.SortOrder)
                    .ThenBy(bouquet => bouquet.CreatedAt)
                    .ThenBy(bouquet => bouquet.Id)
                    .Select(bouquet => bouquet.Id)
                    .ToListAsync(cancellationToken),

                ContentType.AudioStream => await _context.BouquetAudioStreams
                    .AsNoTracking()
                    .Where(row => row.AudioStreamId == contentId)
                    .Join(bouquets, row => row.BouquetId, bouquet => bouquet.Id, (_, bouquet) => bouquet)
                    .OrderBy(bouquet => bouquet.SortOrder)
                    .ThenBy(bouquet => bouquet.CreatedAt)
                    .ThenBy(bouquet => bouquet.Id)
                    .Select(bouquet => bouquet.Id)
                    .ToListAsync(cancellationToken),

                ContentType.VideoOnDemand => await _context.BouquetVodItems
                    .AsNoTracking()
                    .Where(row => row.VodItemId == contentId)
                    .Join(bouquets, row => row.BouquetId, bouquet => bouquet.Id, (_, bouquet) => bouquet)
                    .OrderBy(bouquet => bouquet.SortOrder)
                    .ThenBy(bouquet => bouquet.CreatedAt)
                    .ThenBy(bouquet => bouquet.Id)
                    .Select(bouquet => bouquet.Id)
                    .ToListAsync(cancellationToken),

                ContentType.Series => await _context.BouquetSeries
                    .AsNoTracking()
                    .Where(row => row.SeriesId == contentId)
                    .Join(bouquets, row => row.BouquetId, bouquet => bouquet.Id, (_, bouquet) => bouquet)
                    .OrderBy(bouquet => bouquet.SortOrder)
                    .ThenBy(bouquet => bouquet.CreatedAt)
                    .ThenBy(bouquet => bouquet.Id)
                    .Select(bouquet => bouquet.Id)
                    .ToListAsync(cancellationToken),

                _ => throw Unsupported(type)
            };
        }

        /// <inheritdoc />
        public Task SetItemBouquetIdsAsync(
            ContentType type,
            Guid contentId,
            IReadOnlyList<Guid> bouquetIds,
            CancellationToken cancellationToken = default)
            => type switch
            {
                ContentType.VideoStream => SetVideoBouquetIdsAsync(contentId, bouquetIds, cancellationToken),
                ContentType.AudioStream => SetAudioBouquetIdsAsync(contentId, bouquetIds, cancellationToken),
                ContentType.VideoOnDemand => SetVodBouquetIdsAsync(contentId, bouquetIds, cancellationToken),
                ContentType.Series => SetSeriesBouquetIdsAsync(contentId, bouquetIds, cancellationToken),
                _ => throw Unsupported(type)
            };

        /// <inheritdoc />
        public async Task<IReadOnlySet<Guid>> GetExistingIdsAsync(
            IReadOnlyCollection<Guid> bouquetIds,
            CancellationToken cancellationToken = default)
        {
            if (bouquetIds.Count == 0)
            {
                return new HashSet<Guid>();
            }

            var ids = bouquetIds.Distinct().ToList();

            var found = await _context.Bouquets
                .AsNoTracking()
                .Where(entry => ids.Contains(entry.Id))
                .Select(entry => entry.Id)
                .ToListAsync(cancellationToken);

            return found.ToHashSet();
        }

        /// <summary>Counts membership rows per bouquet for one of the four kinds.</summary>
        private static async Task<Dictionary<Guid, int>> CountRowsAsync<TEntity>(
            DbSet<TEntity> rows,
            CancellationToken cancellationToken)
            where TEntity : class
            => await rows
                .AsNoTracking()
                .GroupBy(row => EF.Property<Guid>(row, nameof(BouquetVideoStreamEntity.BouquetId)))
                .Select(group => new { BouquetId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(entry => entry.BouquetId, entry => entry.Count, cancellationToken);

        /// <summary>Reads what one bouquet holds of one kind, in the order it holds it.</summary>
        private async Task<IReadOnlyList<BouquetMember>> ReadVideoMembersAsync(
            Guid bouquetId,
            CancellationToken cancellationToken)
            => await _context.BouquetVideoStreams
                .AsNoTracking()
                .Where(row => row.BouquetId == bouquetId)
                .OrderBy(row => row.SortOrder)
                .ThenBy(row => row.CreatedAt)
                .ThenBy(row => row.Id)
                .Join(
                    _context.VideoStreams.AsNoTracking(),
                    row => row.VideoStreamId,
                    content => content.Id,
                    (_, content) => new BouquetMember
                    {
                        Id = content.Id,
                        Name = content.Name,
                        CategoryId = content.CategoryId,
                        CategoryName = content.Category!.Name
                    })
                .ToListAsync(cancellationToken);

        /// <summary>Reads what one bouquet holds of one kind, in the order it holds it.</summary>
        private async Task<IReadOnlyList<BouquetMember>> ReadAudioMembersAsync(
            Guid bouquetId,
            CancellationToken cancellationToken)
            => await _context.BouquetAudioStreams
                .AsNoTracking()
                .Where(row => row.BouquetId == bouquetId)
                .OrderBy(row => row.SortOrder)
                .ThenBy(row => row.CreatedAt)
                .ThenBy(row => row.Id)
                .Join(
                    _context.AudioStreams.AsNoTracking(),
                    row => row.AudioStreamId,
                    content => content.Id,
                    (_, content) => new BouquetMember
                    {
                        Id = content.Id,
                        Name = content.Name,
                        CategoryId = content.CategoryId,
                        CategoryName = content.Category!.Name
                    })
                .ToListAsync(cancellationToken);

        /// <summary>Reads what one bouquet holds of one kind, in the order it holds it.</summary>
        private async Task<IReadOnlyList<BouquetMember>> ReadVodMembersAsync(
            Guid bouquetId,
            CancellationToken cancellationToken)
            => await _context.BouquetVodItems
                .AsNoTracking()
                .Where(row => row.BouquetId == bouquetId)
                .OrderBy(row => row.SortOrder)
                .ThenBy(row => row.CreatedAt)
                .ThenBy(row => row.Id)
                .Join(
                    _context.VodItems.AsNoTracking(),
                    row => row.VodItemId,
                    content => content.Id,
                    (_, content) => new BouquetMember
                    {
                        Id = content.Id,
                        Name = content.Name,
                        CategoryId = content.CategoryId,
                        CategoryName = content.Category!.Name
                    })
                .ToListAsync(cancellationToken);

        /// <summary>Reads what one bouquet holds of one kind, in the order it holds it.</summary>
        private async Task<IReadOnlyList<BouquetMember>> ReadSeriesMembersAsync(
            Guid bouquetId,
            CancellationToken cancellationToken)
            => await _context.BouquetSeries
                .AsNoTracking()
                .Where(row => row.BouquetId == bouquetId)
                .OrderBy(row => row.SortOrder)
                .ThenBy(row => row.CreatedAt)
                .ThenBy(row => row.Id)
                .Join(
                    _context.Series.AsNoTracking(),
                    row => row.SeriesId,
                    content => content.Id,
                    (_, content) => new BouquetMember
                    {
                        Id = content.Id,
                        Name = content.Name,
                        CategoryId = content.CategoryId,
                        CategoryName = content.Category!.Name
                    })
                .ToListAsync(cancellationToken);

        /// <summary>Reads everything of one kind the bouquet does not hold, ordered by name.</summary>
        private async Task<IReadOnlyList<BouquetMember>> ReadVideoAvailableAsync(
            Guid bouquetId,
            CancellationToken cancellationToken)
            => await _context.VideoStreams
                .AsNoTracking()
                .Where(content => !_context.BouquetVideoStreams
                    .Any(row => row.BouquetId == bouquetId && row.VideoStreamId == content.Id))
                .OrderBy(content => content.Name)
                .ThenBy(content => content.Id)
                .Select(content => new BouquetMember
                    {
                        Id = content.Id,
                        Name = content.Name,
                        CategoryId = content.CategoryId,
                        CategoryName = content.Category!.Name
                    })
                .ToListAsync(cancellationToken);

        /// <summary>Reads everything of one kind the bouquet does not hold, ordered by name.</summary>
        private async Task<IReadOnlyList<BouquetMember>> ReadAudioAvailableAsync(
            Guid bouquetId,
            CancellationToken cancellationToken)
            => await _context.AudioStreams
                .AsNoTracking()
                .Where(content => !_context.BouquetAudioStreams
                    .Any(row => row.BouquetId == bouquetId && row.AudioStreamId == content.Id))
                .OrderBy(content => content.Name)
                .ThenBy(content => content.Id)
                .Select(content => new BouquetMember
                    {
                        Id = content.Id,
                        Name = content.Name,
                        CategoryId = content.CategoryId,
                        CategoryName = content.Category!.Name
                    })
                .ToListAsync(cancellationToken);

        /// <summary>Reads everything of one kind the bouquet does not hold, ordered by name.</summary>
        private async Task<IReadOnlyList<BouquetMember>> ReadVodAvailableAsync(
            Guid bouquetId,
            CancellationToken cancellationToken)
            => await _context.VodItems
                .AsNoTracking()
                .Where(content => !_context.BouquetVodItems
                    .Any(row => row.BouquetId == bouquetId && row.VodItemId == content.Id))
                .OrderBy(content => content.Name)
                .ThenBy(content => content.Id)
                .Select(content => new BouquetMember
                    {
                        Id = content.Id,
                        Name = content.Name,
                        CategoryId = content.CategoryId,
                        CategoryName = content.Category!.Name
                    })
                .ToListAsync(cancellationToken);

        /// <summary>Reads everything of one kind the bouquet does not hold, ordered by name.</summary>
        private async Task<IReadOnlyList<BouquetMember>> ReadSeriesAvailableAsync(
            Guid bouquetId,
            CancellationToken cancellationToken)
            => await _context.Series
                .AsNoTracking()
                .Where(content => !_context.BouquetSeries
                    .Any(row => row.BouquetId == bouquetId && row.SeriesId == content.Id))
                .OrderBy(content => content.Name)
                .ThenBy(content => content.Id)
                .Select(content => new BouquetMember
                    {
                        Id = content.Id,
                        Name = content.Name,
                        CategoryId = content.CategoryId,
                        CategoryName = content.Category!.Name
                    })
                .ToListAsync(cancellationToken);

        /// <summary>Stages the whole video list of one bouquet: membership and positions 1..N.</summary>
        private async Task SetVideoContentAsync(
            Guid bouquetId,
            IReadOnlyList<Guid> contentIds,
            CancellationToken cancellationToken)
        {
            var rows = await _context.BouquetVideoStreams
                .Where(row => row.BouquetId == bouquetId)
                .ToListAsync(cancellationToken);

            var existing = rows.ToDictionary(row => row.VideoStreamId);
            var kept = contentIds.ToHashSet();

            _context.BouquetVideoStreams.RemoveRange(rows.Where(row => !kept.Contains(row.VideoStreamId)));

            for (var index = 0; index < contentIds.Count; index++)
            {
                if (existing.TryGetValue(contentIds[index], out var row))
                {
                    row.SortOrder = index + 1;
                }
                else
                {
                    _context.BouquetVideoStreams.Add(new BouquetVideoStreamEntity
                    {
                        BouquetId = bouquetId,
                        VideoStreamId = contentIds[index],
                        SortOrder = index + 1
                    });
                }
            }
        }

        /// <summary>Stages the whole audio list of one bouquet: membership and positions 1..N.</summary>
        private async Task SetAudioContentAsync(
            Guid bouquetId,
            IReadOnlyList<Guid> contentIds,
            CancellationToken cancellationToken)
        {
            var rows = await _context.BouquetAudioStreams
                .Where(row => row.BouquetId == bouquetId)
                .ToListAsync(cancellationToken);

            var existing = rows.ToDictionary(row => row.AudioStreamId);
            var kept = contentIds.ToHashSet();

            _context.BouquetAudioStreams.RemoveRange(rows.Where(row => !kept.Contains(row.AudioStreamId)));

            for (var index = 0; index < contentIds.Count; index++)
            {
                if (existing.TryGetValue(contentIds[index], out var row))
                {
                    row.SortOrder = index + 1;
                }
                else
                {
                    _context.BouquetAudioStreams.Add(new BouquetAudioStreamEntity
                    {
                        BouquetId = bouquetId,
                        AudioStreamId = contentIds[index],
                        SortOrder = index + 1
                    });
                }
            }
        }

        /// <summary>Stages the whole on-demand list of one bouquet: membership and positions 1..N.</summary>
        private async Task SetVodContentAsync(
            Guid bouquetId,
            IReadOnlyList<Guid> contentIds,
            CancellationToken cancellationToken)
        {
            var rows = await _context.BouquetVodItems
                .Where(row => row.BouquetId == bouquetId)
                .ToListAsync(cancellationToken);

            var existing = rows.ToDictionary(row => row.VodItemId);
            var kept = contentIds.ToHashSet();

            _context.BouquetVodItems.RemoveRange(rows.Where(row => !kept.Contains(row.VodItemId)));

            for (var index = 0; index < contentIds.Count; index++)
            {
                if (existing.TryGetValue(contentIds[index], out var row))
                {
                    row.SortOrder = index + 1;
                }
                else
                {
                    _context.BouquetVodItems.Add(new BouquetVodItemEntity
                    {
                        BouquetId = bouquetId,
                        VodItemId = contentIds[index],
                        SortOrder = index + 1
                    });
                }
            }
        }

        /// <summary>Stages the whole series list of one bouquet: membership and positions 1..N.</summary>
        private async Task SetSeriesContentAsync(
            Guid bouquetId,
            IReadOnlyList<Guid> contentIds,
            CancellationToken cancellationToken)
        {
            var rows = await _context.BouquetSeries
                .Where(row => row.BouquetId == bouquetId)
                .ToListAsync(cancellationToken);

            var existing = rows.ToDictionary(row => row.SeriesId);
            var kept = contentIds.ToHashSet();

            _context.BouquetSeries.RemoveRange(rows.Where(row => !kept.Contains(row.SeriesId)));

            for (var index = 0; index < contentIds.Count; index++)
            {
                if (existing.TryGetValue(contentIds[index], out var row))
                {
                    row.SortOrder = index + 1;
                }
                else
                {
                    _context.BouquetSeries.Add(new BouquetSeriesEntity
                    {
                        BouquetId = bouquetId,
                        SeriesId = contentIds[index],
                        SortOrder = index + 1
                    });
                }
            }
        }

        /// <summary>
        /// Stages one video's membership across bouquets, appending it where it is new.
        /// </summary>
        private async Task SetVideoBouquetIdsAsync(
            Guid contentId,
            IReadOnlyList<Guid> bouquetIds,
            CancellationToken cancellationToken)
        {
            var wanted = bouquetIds.Distinct().ToList();
            var rows = await _context.BouquetVideoStreams
                .Where(row => row.VideoStreamId == contentId)
                .ToListAsync(cancellationToken);

            var current = rows.Select(row => row.BouquetId).ToHashSet();

            _context.BouquetVideoStreams.RemoveRange(rows.Where(row => !wanted.Contains(row.BouquetId)));

            foreach (var bouquetId in wanted.Where(id => !current.Contains(id)))
            {
                _context.BouquetVideoStreams.Add(new BouquetVideoStreamEntity
                {
                    BouquetId = bouquetId,
                    VideoStreamId = contentId,
                    SortOrder = await NextVideoPositionAsync(bouquetId, cancellationToken)
                });
            }
        }

        /// <summary>
        /// Stages one audio stream's membership across bouquets, appending it where it is new.
        /// </summary>
        private async Task SetAudioBouquetIdsAsync(
            Guid contentId,
            IReadOnlyList<Guid> bouquetIds,
            CancellationToken cancellationToken)
        {
            var wanted = bouquetIds.Distinct().ToList();
            var rows = await _context.BouquetAudioStreams
                .Where(row => row.AudioStreamId == contentId)
                .ToListAsync(cancellationToken);

            var current = rows.Select(row => row.BouquetId).ToHashSet();

            _context.BouquetAudioStreams.RemoveRange(rows.Where(row => !wanted.Contains(row.BouquetId)));

            foreach (var bouquetId in wanted.Where(id => !current.Contains(id)))
            {
                _context.BouquetAudioStreams.Add(new BouquetAudioStreamEntity
                {
                    BouquetId = bouquetId,
                    AudioStreamId = contentId,
                    SortOrder = await NextAudioPositionAsync(bouquetId, cancellationToken)
                });
            }
        }

        /// <summary>
        /// Stages one on-demand video's membership across bouquets, appending it where it is new.
        /// </summary>
        private async Task SetVodBouquetIdsAsync(
            Guid contentId,
            IReadOnlyList<Guid> bouquetIds,
            CancellationToken cancellationToken)
        {
            var wanted = bouquetIds.Distinct().ToList();
            var rows = await _context.BouquetVodItems
                .Where(row => row.VodItemId == contentId)
                .ToListAsync(cancellationToken);

            var current = rows.Select(row => row.BouquetId).ToHashSet();

            _context.BouquetVodItems.RemoveRange(rows.Where(row => !wanted.Contains(row.BouquetId)));

            foreach (var bouquetId in wanted.Where(id => !current.Contains(id)))
            {
                _context.BouquetVodItems.Add(new BouquetVodItemEntity
                {
                    BouquetId = bouquetId,
                    VodItemId = contentId,
                    SortOrder = await NextVodPositionAsync(bouquetId, cancellationToken)
                });
            }
        }

        /// <summary>
        /// Stages one series' membership across bouquets, appending it where it is new.
        /// </summary>
        private async Task SetSeriesBouquetIdsAsync(
            Guid contentId,
            IReadOnlyList<Guid> bouquetIds,
            CancellationToken cancellationToken)
        {
            var wanted = bouquetIds.Distinct().ToList();
            var rows = await _context.BouquetSeries
                .Where(row => row.SeriesId == contentId)
                .ToListAsync(cancellationToken);

            var current = rows.Select(row => row.BouquetId).ToHashSet();

            _context.BouquetSeries.RemoveRange(rows.Where(row => !wanted.Contains(row.BouquetId)));

            foreach (var bouquetId in wanted.Where(id => !current.Contains(id)))
            {
                _context.BouquetSeries.Add(new BouquetSeriesEntity
                {
                    BouquetId = bouquetId,
                    SeriesId = contentId,
                    SortOrder = await NextSeriesPositionAsync(bouquetId, cancellationToken)
                });
            }
        }

        /// <summary>
        /// The position a video appended to a bouquet takes: one after the last it holds, or the
        /// first position when it holds none. Appending rather than inserting is the rule - a
        /// tick in a picker must never re-order a list the operator arranged elsewhere.
        /// </summary>
        private async Task<int> NextVideoPositionAsync(Guid bouquetId, CancellationToken cancellationToken)
        {
            // **The tracked rows count**, not only the stored ones. An import stages one
            // membership per created row inside a single unit of work, so on the second
            // iteration the first one is in the change tracker and not yet in the database -
            // a Max over the table alone answered 0 for every row and the whole batch
            // landed on position 1, which is a unique-index violation waiting for the save.
            var stored = await _context.BouquetVideoStreams
                .Where(row => row.BouquetId == bouquetId)
                .MaxAsync(row => (int?)row.SortOrder, cancellationToken) ?? 0;

            var staged = _context.BouquetVideoStreams.Local
                .Where(row => row.BouquetId == bouquetId)
                .Select(row => row.SortOrder)
                .DefaultIfEmpty(0)
                .Max();

            return Math.Max(stored, staged) + 1;
        }

        /// <summary>The position an audio stream appended to a bouquet takes.</summary>
        private async Task<int> NextAudioPositionAsync(Guid bouquetId, CancellationToken cancellationToken)
        {
            // **The tracked rows count**, not only the stored ones. An import stages one
            // membership per created row inside a single unit of work, so on the second
            // iteration the first one is in the change tracker and not yet in the database -
            // a Max over the table alone answered 0 for every row and the whole batch
            // landed on position 1, which is a unique-index violation waiting for the save.
            var stored = await _context.BouquetAudioStreams
                .Where(row => row.BouquetId == bouquetId)
                .MaxAsync(row => (int?)row.SortOrder, cancellationToken) ?? 0;

            var staged = _context.BouquetAudioStreams.Local
                .Where(row => row.BouquetId == bouquetId)
                .Select(row => row.SortOrder)
                .DefaultIfEmpty(0)
                .Max();

            return Math.Max(stored, staged) + 1;
        }

        /// <summary>The position an on-demand video appended to a bouquet takes.</summary>
        private async Task<int> NextVodPositionAsync(Guid bouquetId, CancellationToken cancellationToken)
        {
            // **The tracked rows count**, not only the stored ones. An import stages one
            // membership per created row inside a single unit of work, so on the second
            // iteration the first one is in the change tracker and not yet in the database -
            // a Max over the table alone answered 0 for every row and the whole batch
            // landed on position 1, which is a unique-index violation waiting for the save.
            var stored = await _context.BouquetVodItems
                .Where(row => row.BouquetId == bouquetId)
                .MaxAsync(row => (int?)row.SortOrder, cancellationToken) ?? 0;

            var staged = _context.BouquetVodItems.Local
                .Where(row => row.BouquetId == bouquetId)
                .Select(row => row.SortOrder)
                .DefaultIfEmpty(0)
                .Max();

            return Math.Max(stored, staged) + 1;
        }

        /// <summary>The position a series appended to a bouquet takes.</summary>
        private async Task<int> NextSeriesPositionAsync(Guid bouquetId, CancellationToken cancellationToken)
        {
            // **The tracked rows count**, not only the stored ones. An import stages one
            // membership per created row inside a single unit of work, so on the second
            // iteration the first one is in the change tracker and not yet in the database -
            // a Max over the table alone answered 0 for every row and the whole batch
            // landed on position 1, which is a unique-index violation waiting for the save.
            var stored = await _context.BouquetSeries
                .Where(row => row.BouquetId == bouquetId)
                .MaxAsync(row => (int?)row.SortOrder, cancellationToken) ?? 0;

            var staged = _context.BouquetSeries.Local
                .Where(row => row.BouquetId == bouquetId)
                .Select(row => row.SortOrder)
                .DefaultIfEmpty(0)
                .Max();

            return Math.Max(stored, staged) + 1;
        }

        /// <summary>The refusal for a kind of content no membership table carries.</summary>
        private static ArgumentOutOfRangeException Unsupported(ContentType type)
            => new(nameof(type), type, "No bouquet membership table exists for that kind of content.");
    }
}
