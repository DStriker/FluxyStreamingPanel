using Fluxy.Core.Content.Repositories;
using Fluxy.Core.Content.Rows;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="ISourceReplacementRepository"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every read is a candidate read.</b> The search text is used as a plain
    /// <c>Contains</c> filter so the database hands back only rows that can contain it. That
    /// is a narrowing, not the rule: the comparison the replacement actually applies is
    /// ordinal and case sensitive, and the service re-checks each candidate against it. A
    /// database collation that widened the filter only ever adds work, never a wrong write.
    /// </para>
    /// <para>
    /// <b>Every write mutates the row in place and nothing is saved here.</b> The set intents
    /// change the address column of an existing tracked row and leave its identifier, its
    /// position and its audit columns to the tracker and the caller's single commit. No row is
    /// deleted and re-inserted, no <c>ExecuteUpdate</c> runs, and no raw SQL is used: a run
    /// that replaces a host must not move a source to the end of its failover chain or give it
    /// a new identity.
    /// </para>
    /// </remarks>
    internal sealed class SourceReplacementRepository : ISourceReplacementRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceReplacementRepository"/> class.
        /// </summary>
        /// <param name="context">Context the candidates are read from and the writes are staged in.</param>
        public SourceReplacementRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<StoredStreamSourceRow>> GetStreamSourceCandidatesAsync(
            string find,
            CancellationToken cancellationToken = default)
            => await _context.VideoStreamSources
                .AsNoTracking()
                .Where(source => source.Url.Contains(find))
                .OrderBy(source => source.Stream!.Name)
                .ThenBy(source => source.StreamId)
                .ThenBy(source => source.Position)
                .Select(source => new StoredStreamSourceRow
                {
                    StreamId = source.StreamId,
                    StreamName = source.Stream!.Name,
                    Position = source.Position,
                    Url = source.Url
                })
                .ToListAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<IReadOnlyList<StoredAudioSourceRow>> GetAudioSourceCandidatesAsync(
            string find,
            CancellationToken cancellationToken = default)
            => await _context.AudioStreamSources
                .AsNoTracking()
                .Where(source => source.Url.Contains(find))
                .OrderBy(source => source.Stream!.Name)
                .ThenBy(source => source.StreamId)
                .ThenBy(source => source.Position)
                .Select(source => new StoredAudioSourceRow
                {
                    StreamId = source.StreamId,
                    StreamName = source.Stream!.Name,
                    Position = source.Position,
                    Url = source.Url
                })
                .ToListAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<IReadOnlyList<StoredVodSourceRow>> GetVodSourceCandidatesAsync(
            string find,
            CancellationToken cancellationToken = default)
            => await _context.VodItems
                .AsNoTracking()
                .Where(item => item.Source.Contains(find))
                .OrderBy(item => item.Name)
                .ThenBy(item => item.Id)
                .Select(item => new StoredVodSourceRow
                {
                    Id = item.Id,
                    Name = item.Name,
                    Source = item.Source
                })
                .ToListAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<IReadOnlyList<StoredEpisodeSourceRow>> GetEpisodeSourceCandidatesAsync(
            string find,
            CancellationToken cancellationToken = default)
            => await _context.Episodes
                .AsNoTracking()
                .Where(episode => episode.Source.Contains(find))
                .OrderBy(episode => episode.SeriesId)
                .ThenBy(episode => episode.SeasonNumber)
                .ThenBy(episode => episode.EpisodeNumber)
                .Select(episode => new StoredEpisodeSourceRow
                {
                    Id = episode.Id,
                    Name = episode.Name == null || episode.Name == string.Empty
                        ? "S" + episode.SeasonNumber + "E" + episode.EpisodeNumber
                        : episode.Name,
                    Source = episode.Source
                })
                .ToListAsync(cancellationToken);

        /// <inheritdoc />
        public async Task SetStreamSourcesAsync(
            Guid streamId,
            IReadOnlyDictionary<int, string> urlsByPosition,
            CancellationToken cancellationToken = default)
        {
            if (urlsByPosition.Count == 0)
            {
                return;
            }

            var positions = urlsByPosition.Keys.ToArray();

            var rows = await _context.VideoStreamSources
                .Where(source => source.StreamId == streamId)
                .Where(source => positions.Contains(source.Position))
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                if (urlsByPosition.TryGetValue(row.Position, out var url))
                {
                    row.Url = url;
                }
            }
        }

        /// <inheritdoc />
        public async Task SetAudioSourcesAsync(
            Guid streamId,
            IReadOnlyDictionary<int, string> urlsByPosition,
            CancellationToken cancellationToken = default)
        {
            if (urlsByPosition.Count == 0)
            {
                return;
            }

            var positions = urlsByPosition.Keys.ToArray();

            var rows = await _context.AudioStreamSources
                .Where(source => source.StreamId == streamId)
                .Where(source => positions.Contains(source.Position))
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                if (urlsByPosition.TryGetValue(row.Position, out var url))
                {
                    row.Url = url;
                }
            }
        }

        /// <inheritdoc />
        public async Task SetVodSourceAsync(
            Guid id,
            string source,
            CancellationToken cancellationToken = default)
        {
            var item = await _context.VodItems
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            if (item is not null)
            {
                item.Source = source;
            }
        }

        /// <inheritdoc />
        public async Task SetEpisodeSourceAsync(
            Guid id,
            string source,
            CancellationToken cancellationToken = default)
        {
            var episode = await _context.Episodes
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            if (episode is not null)
            {
                episode.Source = source;
            }
        }
    }
}
