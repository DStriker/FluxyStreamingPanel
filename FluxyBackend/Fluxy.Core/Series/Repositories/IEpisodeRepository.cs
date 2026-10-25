using Fluxy.Core.Series.Commands;
using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Models;
using Fluxy.Core.Series.ReadModels;

namespace Fluxy.Core.Series.Repositories
{
    /// <summary>
    /// Reads and writes the episodes of one series.
    /// </summary>
    /// <remarks>
    /// Every read and write is scoped to a series identifier. The repository owns the page
    /// query, the number probe and the storage of the rows; the service owns what an episode may
    /// contain and which refusal an outcome deserves.
    /// </remarks>
    public interface IEpisodeRepository
    {
        /// <summary>Reads one filtered page of one series' episodes, counted before paging.</summary>
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

        /// <summary>Finds one episode for a change, with the row left tracked.</summary>
        Task<Episode?> FindForUpdateAsync(
            Guid seriesId,
            Guid episodeId,
            CancellationToken cancellationToken = default);

        /// <summary>Whether the series already holds any episode with the season number.</summary>
        Task<bool> SeasonExistsAsync(
            Guid seriesId,
            int seasonNumber,
            CancellationToken cancellationToken = default);

        /// <summary>Whether another episode of the series already holds the number pair.</summary>
        Task<bool> IsNumberTakenAsync(
            Guid seriesId,
            int seasonNumber,
            int episodeNumber,
            Guid? exceptEpisode,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a new episode in its series.</summary>
        Task<Guid> AddAsync(EpisodeWrite episode, CancellationToken cancellationToken = default);

        /// <summary>Stages a batch of new episodes, left for the caller's single save.</summary>
        Task AddRangeAsync(
            IReadOnlyList<EpisodeWrite> episodes,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a change of the episode's fields.</summary>
        Task<bool> UpdateAsync(
            Guid seriesId,
            Guid episodeId,
            EpisodeWrite episode,
            CancellationToken cancellationToken = default);

        /// <summary>Stages the removal of an episode.</summary>
        Task<bool> RemoveAsync(
            Guid seriesId,
            Guid episodeId,
            CancellationToken cancellationToken = default);
    }
}
