using Fluxy.Core.Series.Commands;
using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Models;
using Fluxy.Core.Series.Outcomes;
using Fluxy.Core.Series.ReadModels;

namespace Fluxy.Core.Series.Interfaces
{
    /// <summary>
    /// Reads and changes the episodes of one series.
    /// </summary>
    /// <remarks>
    /// Every read and write is scoped to a series, because an episode has no meaning outside
    /// the series it belongs to. The series identifier is part of the method rather than only
    /// of the episode row, so an episode of another series is answered as not found instead
    /// of being reachable by a bare identifier.
    /// </remarks>
    public interface IEpisodeService
    {
        /// <summary>Reads one page of one series' episodes.</summary>
        Task<EpisodePage> GetPageAsync(
            Guid seriesId,
            int page,
            int pageSize,
            string? search,
            int? seasonNumber,
            EpisodeStatus? status,
            EpisodeType? type,
            EpisodeSortField sortBy,
            EpisodeSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>Reads one episode in full.</summary>
        Task<Episode?> GetAsync(
            Guid seriesId,
            Guid episodeId,
            CancellationToken cancellationToken = default);

        /// <summary>Creates an episode inside its series.</summary>
        Task<EpisodeOutcome> CreateAsync(
            NewEpisode input,
            CancellationToken cancellationToken = default);

        /// <summary>Changes an episode. Every field a body can name is optional.</summary>
        Task<EpisodeOutcome> UpdateAsync(
            Guid seriesId,
            Guid episodeId,
            EpisodePatch patch,
            CancellationToken cancellationToken = default);

        /// <summary>Deletes an episode.</summary>
        Task<EpisodeOutcome> DeleteAsync(
            Guid seriesId,
            Guid episodeId,
            CancellationToken cancellationToken = default);
    }
}
