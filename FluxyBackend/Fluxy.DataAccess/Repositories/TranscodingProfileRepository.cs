using System.Linq.Expressions;
using Fluxy.Core.Transcoding.Commands;
using Fluxy.Core.Transcoding.Enums;
using Fluxy.Core.Transcoding.Models;
using Fluxy.Core.Transcoding.ReadModels;
using Fluxy.Core.Transcoding.Repositories;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="ITranscodingProfileRepository"/>.
    /// </summary>
    /// <remarks>
    /// Search covers the name, because that is the only text the table draws and the only
    /// column a reader scans by. The order is the name and nothing else: every other field is
    /// an optional ffmpeg option whose order a reader does not compare down a table.
    /// </remarks>
    internal sealed class TranscodingProfileRepository : ITranscodingProfileRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="TranscodingProfileRepository"/> class.
        /// </summary>
        /// <param name="context">Context the profile rows are read from and staged in.</param>
        public TranscodingProfileRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<TranscodingProfilePage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            TranscodingProfileSortField sortBy,
            TranscodingProfileSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            var query = _context.TranscodingProfiles.AsNoTracking();

            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                query = query.Where(profile => profile.Name.ToLower().Contains(needle));
            }

            // Counted under the same predicate the page is cut from, and before the order is
            // applied: a total counted after Skip/Take would describe the page, not the list.
            var total = await query.CountAsync(cancellationToken);

            query = Order(query, sortBy, sortOrder);

            var rows = await query
                .Select(profile => ToView(profile))
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new TranscodingProfilePage
            {
                Items = rows,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public Task<TranscodingProfile?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => _context.TranscodingProfiles
                .AsNoTracking()
                .Where(profile => profile.Id == id)
                .Select(profile => profile.ToModel())
                .FirstOrDefaultAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<IReadOnlyList<TranscodingProfileOption>> GetOptionsAsync(
            CancellationToken cancellationToken = default)
            => await _context.TranscodingProfiles
                .AsNoTracking()
                .OrderBy(profile => profile.Name)
                .Select(profile => new TranscodingProfileOption
                {
                    Id = profile.Id,
                    Name = profile.Name
                })
                .ToListAsync(cancellationToken);

        /// <inheritdoc />
        public Task<bool> IsNameTakenAsync(
            string name,
            Guid? exceptProfile,
            CancellationToken cancellationToken = default)
            => _context.TranscodingProfiles
                .AsNoTracking()
                .AnyAsync(
                    profile => profile.Name == name
                        && (exceptProfile == null || profile.Id != exceptProfile),
                    cancellationToken);

        /// <inheritdoc />
        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.TranscodingProfiles
                .AsNoTracking()
                .AnyAsync(profile => profile.Id == id, cancellationToken);

        /// <inheritdoc />
        /// <remarks>
        /// Four counts rather than a union: the four tables share no column beyond the profile
        /// identifier, and counting each is the query the planner already has an index for. The
        /// rule is the one <c>ContentCategoryRepository.CountContentAsync</c> follows.
        /// </remarks>
        public async Task<int> CountReferencesAsync(
            Guid profileId,
            CancellationToken cancellationToken = default)
        {
            var videoStreams = await _context.VideoStreams
                .AsNoTracking()
                .CountAsync(row => row.TranscodingProfileId == profileId, cancellationToken);

            var audioStreams = await _context.AudioStreams
                .AsNoTracking()
                .CountAsync(row => row.TranscodingProfileId == profileId, cancellationToken);

            var onDemand = await _context.VodItems
                .AsNoTracking()
                .CountAsync(row => row.TranscodingProfileId == profileId, cancellationToken);

            var series = await _context.Series
                .AsNoTracking()
                .CountAsync(row => row.TranscodingProfileId == profileId, cancellationToken);

            return videoStreams + audioStreams + onDemand + series;
        }

        /// <inheritdoc />
        public Task<Guid> AddAsync(
            TranscodingProfileWrite profile,
            CancellationToken cancellationToken = default)
        {
            var entity = new TranscodingProfileEntity { Name = profile.Name };

            Apply(profile, entity);

            _context.TranscodingProfiles.Add(entity);

            return Task.FromResult(entity.Id);
        }

        /// <inheritdoc />
        public async Task<bool> UpdateAsync(
            Guid id,
            TranscodingProfileWrite profile,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.TranscodingProfiles
                .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

            if (entity is null)
            {
                return false;
            }

            // Every option is written, including the nulls: the patch carries the whole set, so
            // a field the operator cleared must become null rather than keep what it held.
            entity.Name = profile.Name;

            Apply(profile, entity);

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> RemoveAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.TranscodingProfiles
                .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

            if (entity is null)
            {
                return false;
            }

            _context.TranscodingProfiles.Remove(entity);
            return true;
        }

        /// <summary>Writes every option of a resolved write onto a row.</summary>
        private static void Apply(TranscodingProfileWrite profile, TranscodingProfileEntity entity)
        {
            entity.VideoCodec = profile.VideoCodec;
            entity.AudioCodec = profile.AudioCodec;
            entity.Preset = profile.Preset;
            entity.VideoProfile = profile.VideoProfile;
            entity.VideoBitrateAvg = profile.VideoBitrateAvg;
            entity.AudioBitrateAvg = profile.AudioBitrateAvg;
            entity.MinBitrateTolerance = profile.MinBitrateTolerance;
            entity.MaxBitrateTolerance = profile.MaxBitrateTolerance;
            entity.BufferSize = profile.BufferSize;
            entity.Crf = profile.Crf;
            entity.Width = profile.Width;
            entity.Height = profile.Height;
            entity.AspectRatio = profile.AspectRatio;
            entity.Framerate = profile.Framerate;
            entity.AudioSampleRate = profile.AudioSampleRate;
            entity.AudioChannels = profile.AudioChannels;
            entity.Threads = profile.Threads;
            entity.LogoUrl = profile.LogoUrl;
            entity.RedactionX = profile.RedactionX;
            entity.RedactionY = profile.RedactionY;
            entity.RedactionWidth = profile.RedactionWidth;
            entity.RedactionHeight = profile.RedactionHeight;
        }

        /// <summary>Projects a row into the shape a response is built from.</summary>
        private static TranscodingProfileView ToView(TranscodingProfileEntity profile) => new()
        {
            Id = profile.Id,
            Name = profile.Name,
            VideoCodec = profile.VideoCodec,
            AudioCodec = profile.AudioCodec,
            Preset = profile.Preset,
            VideoProfile = profile.VideoProfile,
            VideoBitrateAvg = profile.VideoBitrateAvg,
            AudioBitrateAvg = profile.AudioBitrateAvg,
            MinBitrateTolerance = profile.MinBitrateTolerance,
            MaxBitrateTolerance = profile.MaxBitrateTolerance,
            BufferSize = profile.BufferSize,
            Crf = profile.Crf,
            Width = profile.Width,
            Height = profile.Height,
            AspectRatio = profile.AspectRatio,
            Framerate = profile.Framerate,
            AudioSampleRate = profile.AudioSampleRate,
            AudioChannels = profile.AudioChannels,
            Threads = profile.Threads,
            LogoUrl = profile.LogoUrl,
            RedactionX = profile.RedactionX,
            RedactionY = profile.RedactionY,
            RedactionWidth = profile.RedactionWidth,
            RedactionHeight = profile.RedactionHeight
        };

        private static IQueryable<TranscodingProfileEntity> Order(
            IQueryable<TranscodingProfileEntity> query,
            TranscodingProfileSortField field,
            TranscodingProfileSortOrder order)
        {
            var descending = order is TranscodingProfileSortOrder.Descending;

            // The name is the only column the enum offers; the identifier is the tie-break,
            // because two rows written in the same transaction share a stamp and without it two
            // pages could both show or both skip a row.
            return Chain(
                query,
                profile => profile.Name,
                profile => profile.Id,
                descending,
                field);
        }

        private static IOrderedQueryable<TEntity> Chain<TEntity, TKey1, TKey2>(
            IQueryable<TEntity> source,
            Expression<Func<TEntity, TKey1>> primary,
            Expression<Func<TEntity, TKey2>> tieBreak,
            bool descending,
            TranscodingProfileSortField field)
        {
            // The parameter is here so a second sortable column added later has somewhere to be
            // matched against; today every branch orders by name.
            _ = field;

            var ordered = descending
                ? source.OrderByDescending(primary)
                : source.OrderBy(primary);

            return descending
                ? ordered.ThenByDescending(tieBreak)
                : ordered.ThenBy(tieBreak);
        }
    }
}
