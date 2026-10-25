using Fluxy.Application.Content.Utils;
using Fluxy.Application.Series.Utils;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Utils;
using Fluxy.Core.Series.Commands;
using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Interfaces;
using Fluxy.Core.Series.Models;
using Fluxy.Core.Series.Outcomes;
using Fluxy.Core.Series.ReadModels;
using Fluxy.Core.Series.Repositories;

namespace Fluxy.Application.Series.Services
{
    /// <summary>
    /// Reads and changes the episodes of one series.
    /// </summary>
    /// <remarks>
    /// The season-and-number pair is unique inside a series, and the rule lives here so that no
    /// second endpoint reaching the same row can forget it. The transport still checks that the
    /// series exists before it reaches this service, which is why every method can treat the
    /// series identifier as already known.
    /// </remarks>
    public sealed class EpisodeService : IEpisodeService
    {
        private readonly IEpisodeRepository _episodes;
        private readonly ISeriesRepository _series;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="EpisodeService"/> class.
        /// </summary>
        /// <param name="episodes">Repository holding the episode rows.</param>
        /// <param name="series">Repository used to check that the series exists.</param>
        /// <param name="unitOfWork">Commits the changes one operation stages.</param>
        public EpisodeService(
            IEpisodeRepository episodes,
            ISeriesRepository series,
            IUnitOfWork unitOfWork)
        {
            _episodes = episodes;
            _series = series;
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public Task<EpisodePage> GetPageAsync(
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
            => _episodes.GetPageAsync(
                seriesId,
                page,
                pageSize,
                search,
                seasonNumber,
                status,
                type,
                sortBy,
                sortOrder,
                cancellationToken);

        /// <inheritdoc />
        public Task<Episode?> GetAsync(
            Guid seriesId,
            Guid episodeId,
            CancellationToken cancellationToken = default)
            => _episodes.GetAsync(seriesId, episodeId, cancellationToken);

        /// <inheritdoc />
        public async Task<EpisodeOutcome> CreateAsync(
            NewEpisode input,
            CancellationToken cancellationToken = default)
        {
            var name = ContentMetadataRules.NullIfBlank(input.Name);
            var source = input.Source.Trim();
            var imdbId = ContentMetadataRules.NullIfBlank(input.ImdbId);
            var imageUrl = ContentMetadataRules.NullIfBlank(input.ImageUrl);
            var plot = ContentMetadataRules.NullIfBlank(input.Plot);

            var errors = EpisodeRules.Validate(
                input.SeriesId,
                input.SeasonNumber,
                input.EpisodeNumber,
                name,
                source,
                input.Status,
                input.Type,
                imdbId,
                imageUrl,
                plot,
                input.ReleaseDate,
                input.Rating);

            if (errors.Count > 0)
            {
                return Invalid(errors, input.SeriesId, Guid.Empty);
            }

            if (await _series.ExistsAsync(input.SeriesId, cancellationToken) is false)
            {
                return NotFound(input.SeriesId, Guid.Empty);
            }

            if (await _episodes.IsNumberTakenAsync(
                input.SeriesId,
                input.SeasonNumber,
                input.EpisodeNumber,
                exceptEpisode: null,
                cancellationToken))
            {
                return AlreadyExists(
                    nameof(NewEpisode.EpisodeNumber),
                    input.SeriesId,
                    Guid.Empty);
            }

            ContentMetadataRules.TryParseReleaseDate(input.ReleaseDate, out var releaseDate);

            var id = await _episodes.AddAsync(
                BuildWrite(
                    input.SeriesId,
                    input.SeasonNumber,
                    input.EpisodeNumber,
                    name,
                    source,
                    input.Status,
                    input.Type,
                    imdbId,
                    imageUrl,
                    plot,
                    releaseDate,
                    input.Rating),
                cancellationToken);

            var failure = await _unitOfWork.TrySaveAsync(
                [EpisodeRules.NumberIndexName],
                cancellationToken);

            if (failure is not null)
            {
                return AlreadyExists(
                    nameof(NewEpisode.EpisodeNumber),
                    input.SeriesId,
                    id);
            }

            return new EpisodeOutcome
            {
                Action = EpisodeAction.Created,
                SeriesId = input.SeriesId,
                EpisodeId = id
            };
        }

        /// <inheritdoc />
        public async Task<EpisodeOutcome> UpdateAsync(
            Guid seriesId,
            Guid episodeId,
            EpisodePatch patch,
            CancellationToken cancellationToken = default)
        {
            var episode = await _episodes.FindForUpdateAsync(
                seriesId,
                episodeId,
                cancellationToken);

            if (episode is null)
            {
                return NotFound(seriesId, episodeId);
            }

            var seasonNumber = patch.SeasonNumber ?? episode.SeasonNumber;
            var episodeNumber = patch.EpisodeNumber ?? episode.EpisodeNumber;
            var name = MergeText(patch.Name, episode.Name);
            var source = patch.Source is null ? episode.Source : patch.Source.Trim();
            var status = patch.Status ?? episode.Status;
            var type = patch.Type ?? episode.Type;
            var imdbId = MergeText(patch.ImdbId, episode.ImdbId);
            var imageUrl = MergeText(patch.ImageUrl, episode.ImageUrl);
            var plot = MergeText(patch.Plot, episode.Plot);

            var releaseDate = episode.ReleaseDate;

            if (patch.ReleaseDate is not null)
            {
                if (!ContentMetadataRules.TryParseReleaseDate(patch.ReleaseDate, out var parsedDate))
                {
                    return Invalid(
                        Field(nameof(EpisodePatch.ReleaseDate), EpisodeRules.ReleaseDateRule),
                        seriesId,
                        episodeId);
                }

                releaseDate = parsedDate;
            }

            var rating = episode.Rating;

            if (patch.Rating is not null)
            {
                if (!ContentMetadataRules.TryParseRating(patch.Rating, out var parsedRating))
                {
                    return Invalid(
                        Field(nameof(EpisodePatch.Rating), EpisodeRules.RatingRule),
                        seriesId,
                        episodeId);
                }

                rating = parsedRating;
            }

            var errors = EpisodeRules.Validate(
                seriesId,
                seasonNumber,
                episodeNumber,
                name,
                source,
                status,
                type,
                imdbId,
                imageUrl,
                plot,
                patch.ReleaseDate,
                rating);

            if (errors.Count > 0)
            {
                return Invalid(errors, seriesId, episodeId);
            }

            if (await _episodes.IsNumberTakenAsync(
                seriesId,
                seasonNumber,
                episodeNumber,
                episodeId,
                cancellationToken))
            {
                return AlreadyExists(
                    nameof(EpisodePatch.EpisodeNumber),
                    seriesId,
                    episodeId);
            }

            await _episodes.UpdateAsync(
                seriesId,
                episodeId,
                BuildWrite(
                    seriesId,
                    seasonNumber,
                    episodeNumber,
                    name,
                    source,
                    status,
                    type,
                    imdbId,
                    imageUrl,
                    plot,
                    releaseDate,
                    rating),
                cancellationToken);

            var failure = await _unitOfWork.TrySaveAsync(
                [EpisodeRules.NumberIndexName],
                cancellationToken);

            if (failure is not null)
            {
                return AlreadyExists(
                    nameof(EpisodePatch.EpisodeNumber),
                    seriesId,
                    episodeId);
            }

            return new EpisodeOutcome
            {
                Action = EpisodeAction.Updated,
                SeriesId = seriesId,
                EpisodeId = episodeId
            };
        }

        /// <inheritdoc />
        public async Task<EpisodeOutcome> DeleteAsync(
            Guid seriesId,
            Guid episodeId,
            CancellationToken cancellationToken = default)
        {
            if (!await _episodes.RemoveAsync(seriesId, episodeId, cancellationToken))
            {
                return NotFound(seriesId, episodeId);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new EpisodeOutcome
            {
                Action = EpisodeAction.Removed,
                SeriesId = seriesId,
                EpisodeId = episodeId
            };
        }

        /// <summary>Turns the merged values into the row the repository writes.</summary>
        private static EpisodeWrite BuildWrite(
            Guid seriesId,
            int seasonNumber,
            int episodeNumber,
            string? name,
            string source,
            EpisodeStatus status,
            EpisodeType type,
            string? imdbId,
            string? imageUrl,
            string? plot,
            DateOnly? releaseDate,
            decimal? rating)
            => new()
            {
                SeriesId = seriesId,
                SeasonNumber = seasonNumber,
                EpisodeNumber = episodeNumber,
                Name = name,
                Source = source,
                Status = status,
                Type = type,
                ImdbId = imdbId,
                ImageUrl = imageUrl,
                Plot = plot,
                ReleaseDate = releaseDate,
                Rating = rating
            };

        /// <summary>
        /// Resolves one nullable text field of a change: absent keeps, empty clears, anything
        /// else is trimmed and stored.
        /// </summary>
        private static string? MergeText(string? sent, string? stored)
            => sent is null ? stored : ContentMetadataRules.NullIfBlank(sent);

        /// <summary>One field's worth of rejections, keyed by the C# property name.</summary>
        private static Dictionary<string, string[]> Field(string name, string[] sentences)
            => new(StringComparer.Ordinal) { [name] = sentences };

        private static EpisodeOutcome NotFound(Guid seriesId, Guid episodeId)
            => new()
            {
                Action = EpisodeAction.NotFound,
                SeriesId = seriesId,
                EpisodeId = episodeId
            };

        private static EpisodeOutcome AlreadyExists(
            string field,
            Guid seriesId,
            Guid episodeId)
            => new()
            {
                Action = EpisodeAction.AlreadyExists,
                SeriesId = seriesId,
                EpisodeId = episodeId,
                Errors = Field(
                    field,
                    ["Another episode of this series already has that season and number."])
            };

        private static EpisodeOutcome Invalid(
            IReadOnlyDictionary<string, string[]> errors,
            Guid seriesId,
            Guid episodeId)
            => new()
            {
                Action = EpisodeAction.Invalid,
                SeriesId = seriesId,
                EpisodeId = episodeId,
                Errors = errors
            };
    }
}
