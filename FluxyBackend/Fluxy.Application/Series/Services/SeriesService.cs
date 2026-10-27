using Fluxy.Application.Content.Utils;
using Fluxy.Application.Series.Utils;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Repositories;
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
    /// Reads the series catalogue and changes it on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// Two rules decide most of what happens here, and they live in this class rather than at
    /// the call sites so that no second endpoint reaching the same action can forget them: a
    /// name is unique <i>within its category</i>, and a patch resolves "absent" against "empty"
    /// before anything is judged, so the rules see the row as it is about to become.
    /// </remarks>
    public sealed class SeriesService : ISeriesService
    {
        private readonly ISeriesRepository _series;
        private readonly IBouquetRepository _bouquets;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeriesService"/> class.
        /// </summary>
        /// <param name="series">Repository holding the series rows.</param>
        /// <param name="bouquets">
        /// Repository that answers whether the bouquets a series names exist, and stages the
        /// membership rows that tie the two together.
        /// </param>
        /// <param name="unitOfWork">Commits the changes one operation stages.</param>
        public SeriesService(
            ISeriesRepository series,
            IBouquetRepository bouquets,
            IUnitOfWork unitOfWork)
        {
            _series = series;
            _bouquets = bouquets;
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public Task<SeriesPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? categoryId,
            SeriesStatus? status,
            SeriesSortField sortBy,
            SeriesSortOrder sortOrder,
            CancellationToken cancellationToken = default)
            => _series.GetPageAsync(
                page,
                pageSize,
                search,
                categoryId,
                status,
                sortBy,
                sortOrder,
                cancellationToken);

        /// <inheritdoc />
        public Task<SeriesDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => _series.GetAsync(id, cancellationToken);

        /// <inheritdoc />
        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
            => _series.ExistsAsync(id, cancellationToken);

        /// <inheritdoc />
        public async Task<SeriesOutcome> CreateAsync(
            NewSeries input,
            CancellationToken cancellationToken = default)
        {
            var name = input.Name.Trim();
            var imdbId = ContentMetadataRules.NullIfBlank(input.ImdbId);
            var posterUrl = ContentMetadataRules.NullIfBlank(input.PosterUrl);
            var backdropUrl = ContentMetadataRules.NullIfBlank(input.BackdropUrl);
            var plot = ContentMetadataRules.NullIfBlank(input.Plot);
            var cast = ContentMetadataRules.NormalizeList(input.Cast);
            var director = ContentMetadataRules.NullIfBlank(input.Director);
            var genres = ContentMetadataRules.NormalizeList(input.Genres);

            var errors = SeriesRules.Validate(
                name,
                input.Status,
                input.CategoryId,
                imdbId,
                posterUrl,
                backdropUrl,
                plot,
                cast,
                director,
                genres,
                input.ReleaseDate,
                input.Rating);

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            ContentMetadataRules.TryParseReleaseDate(input.ReleaseDate, out var releaseDate);

            if (await _series.CategoryIsUsableAsync(input.CategoryId, cancellationToken) is false)
            {
                return Invalid(Field(nameof(NewSeries.CategoryId), SeriesRules.CategoryRule));
            }

            if (await _series.IsNameTakenAsync(
                name,
                input.CategoryId,
                exceptSeries: null,
                cancellationToken))
            {
                return AlreadyExists(nameof(NewSeries.Name));
            }

            // The bouquets are a picker on the series' own form, so a body naming one that does
            // not exist is refused with the field named rather than failing at the write.
            if (!await BouquetMembership.AreKnownAsync(_bouquets, input.BouquetIds, cancellationToken))
            {
                return Invalid(Field(nameof(NewSeries.BouquetIds), SeriesRules.BouquetsRule));
            }

            var id = await _series.AddAsync(
                BuildWrite(
                    name,
                    input.Status,
                    input.CategoryId,
                    imdbId,
                    posterUrl,
                    backdropUrl,
                    plot,
                    cast,
                    director,
                    genres,
                    releaseDate,
                    input.Rating),
                cancellationToken);

            // Staged before the save, so the row and the packages it joined are written by one
            // SaveChanges.
            await BouquetMembership.StageAsync(
                _bouquets,
                ContentType.Series,
                id,
                input.BouquetIds,
                cancellationToken);

            var failure = await _unitOfWork.TrySaveAsync(
                [SeriesRules.NameIndexName],
                cancellationToken);

            if (failure is not null)
            {
                return AlreadyExists(nameof(NewSeries.Name));
            }

            return new SeriesOutcome
            {
                Action = SeriesAction.Created,
                SeriesId = id
            };
        }

        /// <inheritdoc />
        public async Task<SeriesOutcome> UpdateAsync(
            Guid id,
            SeriesPatch patch,
            CancellationToken cancellationToken = default)
        {
            var series = await _series.FindForUpdateAsync(id, cancellationToken);

            if (series is null)
            {
                return NotFound(id);
            }

            // Merged first and judged afterwards, so the rules see the row as it is about to
            // become rather than as it was. Every "absent means leave it alone" of the patch is
            // resolved here, in one place.
            var name = patch.Name is null ? series.Name : patch.Name.Trim();
            var status = patch.Status ?? series.Status;
            var categoryId = patch.CategoryId ?? series.CategoryId;
            var imdbId = MergeText(patch.ImdbId, series.ImdbId);
            var posterUrl = MergeText(patch.PosterUrl, series.PosterUrl);
            var backdropUrl = MergeText(patch.BackdropUrl, series.BackdropUrl);
            var plot = MergeText(patch.Plot, series.Plot);
            var cast = patch.Cast is null
                ? series.Cast
                : ContentMetadataRules.NormalizeList(patch.Cast);
            var director = MergeText(patch.Director, series.Director);
            var genres = patch.Genres is null
                ? series.Genres
                : ContentMetadataRules.NormalizeList(patch.Genres);

            var releaseDate = series.ReleaseDate;

            if (patch.ReleaseDate is not null)
            {
                if (!ContentMetadataRules.TryParseReleaseDate(patch.ReleaseDate, out var parsedDate))
                {
                    return Invalid(
                        Field(nameof(SeriesPatch.ReleaseDate), SeriesRules.ReleaseDateRule),
                        id);
                }

                releaseDate = parsedDate;
            }

            var rating = series.Rating;

            if (patch.Rating is not null)
            {
                if (!ContentMetadataRules.TryParseRating(patch.Rating, out var parsedRating))
                {
                    return Invalid(
                        Field(nameof(SeriesPatch.Rating), SeriesRules.RatingRule),
                        id);
                }

                rating = parsedRating;
            }

            var errors = SeriesRules.Validate(
                name,
                status,
                categoryId,
                imdbId,
                posterUrl,
                backdropUrl,
                plot,
                cast,
                director,
                genres,
                patch.ReleaseDate,
                rating);

            if (errors.Count > 0)
            {
                return Invalid(errors, id);
            }

            if (await _series.CategoryIsUsableAsync(categoryId, cancellationToken) is false)
            {
                return Invalid(Field(nameof(SeriesPatch.CategoryId), SeriesRules.CategoryRule), id);
            }

            if (await _series.IsNameTakenAsync(name, categoryId, id, cancellationToken))
            {
                return AlreadyExists(nameof(SeriesPatch.Name), id);
            }

            // Null keeps every link, an empty list leaves every bouquet.
            if (!await BouquetMembership.AreKnownAsync(_bouquets, patch.BouquetIds, cancellationToken))
            {
                return Invalid(Field(nameof(SeriesPatch.BouquetIds), SeriesRules.BouquetsRule), id);
            }

            await _series.UpdateAsync(
                id,
                BuildWrite(
                    name,
                    status,
                    categoryId,
                    imdbId,
                    posterUrl,
                    backdropUrl,
                    plot,
                    cast,
                    director,
                    genres,
                    releaseDate,
                    rating),
                cancellationToken);

            // Inside the same unit of work as the row itself, so a failure rolls both back.
            await BouquetMembership.StageAsync(
                _bouquets,
                ContentType.Series,
                id,
                patch.BouquetIds,
                cancellationToken);

            var failure = await _unitOfWork.TrySaveAsync(
                [SeriesRules.NameIndexName],
                cancellationToken);

            if (failure is not null)
            {
                return AlreadyExists(nameof(SeriesPatch.Name), id);
            }

            return new SeriesOutcome
            {
                Action = SeriesAction.Updated,
                SeriesId = id
            };
        }

        /// <inheritdoc />
        public async Task<SeriesOutcome> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            if (!await _series.RemoveAsync(id, cancellationToken))
            {
                return NotFound(id);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new SeriesOutcome
            {
                Action = SeriesAction.Removed,
                SeriesId = id
            };
        }

        /// <summary>Turns the merged values into the row the repository writes.</summary>
        private static SeriesWrite BuildWrite(
            string name,
            SeriesStatus status,
            Guid categoryId,
            string? imdbId,
            string? posterUrl,
            string? backdropUrl,
            string? plot,
            IReadOnlyList<string> cast,
            string? director,
            IReadOnlyList<string> genres,
            DateOnly? releaseDate,
            decimal? rating)
            => new()
            {
                Name = name,
                Status = status,
                CategoryId = categoryId,
                ImdbId = imdbId,
                PosterUrl = posterUrl,
                BackdropUrl = backdropUrl,
                Plot = plot,
                Cast = cast,
                Director = director,
                Genres = genres,
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

        private static SeriesOutcome NotFound(Guid id)
            => new() { Action = SeriesAction.NotFound, SeriesId = id };

        private static SeriesOutcome AlreadyExists(string field, Guid id = default)
            => new()
            {
                Action = SeriesAction.AlreadyExists,
                SeriesId = id,
                Errors = Field(field, ["Another series of this category already has that name."])
            };

        private static SeriesOutcome Invalid(
            IReadOnlyDictionary<string, string[]> errors,
            Guid id = default)
            => new() { Action = SeriesAction.Invalid, SeriesId = id, Errors = errors };
    }
}
