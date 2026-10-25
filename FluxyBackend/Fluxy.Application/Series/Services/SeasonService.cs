using Fluxy.Application.Series.Utils;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Utils;
using Fluxy.Core.Series.Commands;
using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Interfaces;
using Fluxy.Core.Series.Outcomes;
using Fluxy.Core.Series.Repositories;
using Fluxy.Core.Series.Utils;
using Microsoft.Extensions.Hosting;

namespace Fluxy.Application.Series.Services
{
    /// <summary>
    /// Creates a whole season by reading a folder of video files.
    /// </summary>
    /// <remarks>
    /// A season is the set of episodes carrying one season number, so the request either
    /// creates every row of a new season or refuses. An existing season is refused rather than
    /// topped up: the form is the way to add a season, and a second folder added to a season
    /// that already has episodes would make "the season was created" a claim about only part of
    /// it. Numbering therefore always starts at one.
    /// </remarks>
    public sealed class SeasonService : ISeasonService
    {
        /// <summary>The sentence an existing season gets.</summary>
        private static readonly string[] SeasonExistsRule =
        [
            "This season already has episodes. Choose another season, or add episodes one at a time."
        ];

        /// <summary>The sentence a folder with no usable video files gets.</summary>
        private static readonly string[] EmptyFolderRule =
            ["The folder contains no video files this application can use."];

        /// <summary>The sentence a folder with too many files gets.</summary>
        private static readonly string[] TooManyFilesRule =
        [
            "The folder holds more than " + SeriesLimits.MaxEpisodesPerSeason +
                " video files, which is more than one season may create at once."
        ];

        private readonly ISeriesRepository _series;
        private readonly IEpisodeRepository _episodes;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHostEnvironment _environment;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeasonService"/> class.
        /// </summary>
        /// <param name="series">Repository used to read the series and its name.</param>
        /// <param name="episodes">Repository the episode rows are staged in.</param>
        /// <param name="unitOfWork">Commits the batch as one operation.</param>
        /// <param name="environment">Environment whose content root resolves a relative path.</param>
        public SeasonService(
            ISeriesRepository series,
            IEpisodeRepository episodes,
            IUnitOfWork unitOfWork,
            IHostEnvironment environment)
        {
            _series = series;
            _episodes = episodes;
            _unitOfWork = unitOfWork;
            _environment = environment;
        }

        /// <inheritdoc />
        public async Task<SeasonOutcome> CreateAsync(
            NewSeason input,
            CancellationToken cancellationToken = default)
        {
            var folderPath = input.FolderPath.Trim();

            var errors = EpisodeRules.ValidateSeason(
                input.SeriesId,
                input.SeasonNumber,
                folderPath,
                input.Status,
                input.Type);

            if (errors.Count > 0)
            {
                return Invalid(errors, input.SeriesId, input.SeasonNumber);
            }

            var series = await _series.GetAsync(input.SeriesId, cancellationToken);

            if (series is null)
            {
                return NotFound(input.SeriesId, input.SeasonNumber);
            }

            if (await _episodes.SeasonExistsAsync(
                input.SeriesId,
                input.SeasonNumber,
                cancellationToken))
            {
                return AlreadyExists(input.SeriesId, input.SeasonNumber);
            }

            var resolved = Path.IsPathRooted(folderPath)
                ? folderPath
                : Path.Combine(_environment.ContentRootPath, folderPath);

            if (!Directory.Exists(resolved))
            {
                return Invalid(
                    Field(nameof(NewSeason.FolderPath), EmptyFolderRule),
                    input.SeriesId,
                    input.SeasonNumber);
            }

            var files = TryScan(resolved);

            if (files is null)
            {
                return Invalid(
                    Field(nameof(NewSeason.FolderPath), EmptyFolderRule),
                    input.SeriesId,
                    input.SeasonNumber);
            }

            if (files.Count == 0)
            {
                return Invalid(
                    Field(nameof(NewSeason.FolderPath), EmptyFolderRule),
                    input.SeriesId,
                    input.SeasonNumber);
            }

            if (files.Count > SeriesLimits.MaxEpisodesPerSeason)
            {
                return Invalid(
                    Field(nameof(NewSeason.FolderPath), TooManyFilesRule),
                    input.SeriesId,
                    input.SeasonNumber);
            }

            var writes = new List<EpisodeWrite>(files.Count);

            for (var index = 0; index < files.Count; index++)
            {
                var fileName = Path.GetFileName(files[index]);
                var episodeNumber = index + 1;

                writes.Add(new EpisodeWrite
                {
                    SeriesId = input.SeriesId,
                    SeasonNumber = input.SeasonNumber,
                    EpisodeNumber = episodeNumber,
                    Name = EpisodeNameBuilder.Build(
                        series.Name,
                        input.SeasonNumber,
                        episodeNumber,
                        input.AddSeasonNumberToName,
                        input.AddEpisodeNumberToName),
                    Source = Path.Combine(folderPath, fileName),
                    Status = input.Status,
                    Type = input.Type
                });
            }

            await _episodes.AddRangeAsync(writes, cancellationToken);

            var failure = await _unitOfWork.TrySaveAsync(
                [EpisodeRules.NumberIndexName],
                cancellationToken);

            if (failure is not null)
            {
                return AlreadyExists(input.SeriesId, input.SeasonNumber);
            }

            return new SeasonOutcome
            {
                Action = SeasonAction.Created,
                SeriesId = input.SeriesId,
                SeasonNumber = input.SeasonNumber,
                AddedCount = writes.Count
            };
        }

        /// <summary>
        /// Reads the folder, answering null when it cannot be read at all. A permission or I/O
        /// failure is a fact about the folder the operator named, not an unhandled server error.
        /// </summary>
        private static IReadOnlyList<string>? TryScan(string resolvedFolder)
        {
            try
            {
                return EpisodeFolderScanner.Scan(resolvedFolder);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static SeasonOutcome NotFound(Guid seriesId, int seasonNumber)            => new()
            {
                Action = SeasonAction.NotFound,
                SeriesId = seriesId,
                SeasonNumber = seasonNumber,
                AddedCount = 0
            };

        private static SeasonOutcome AlreadyExists(Guid seriesId, int seasonNumber)
            => new()
            {
                Action = SeasonAction.AlreadyExists,
                SeriesId = seriesId,
                SeasonNumber = seasonNumber,
                AddedCount = 0,
                Errors = Field(nameof(NewSeason.SeasonNumber), SeasonExistsRule)
            };

        private static SeasonOutcome Invalid(
            IReadOnlyDictionary<string, string[]> errors,
            Guid seriesId,
            int seasonNumber)
            => new()
            {
                Action = SeasonAction.Invalid,
                SeriesId = seriesId,
                SeasonNumber = seasonNumber,
                AddedCount = 0,
                Errors = errors
            };

        private static Dictionary<string, string[]> Field(string name, string[] sentences)
            => new(StringComparer.Ordinal) { [name] = sentences };
    }
}
