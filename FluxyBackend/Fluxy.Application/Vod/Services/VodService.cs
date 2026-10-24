using Fluxy.Application.Vod.Utils;
using Fluxy.Core.GeoIp.Utils;
using Fluxy.Core.Persistence.Exceptions;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Utils;
using Fluxy.Core.Vod.Commands;
using Fluxy.Core.Vod.Enums;
using Fluxy.Core.Vod.Interfaces;
using Fluxy.Core.Vod.Models;
using Fluxy.Core.Vod.Outcomes;
using Fluxy.Core.Vod.ReadModels;
using Fluxy.Core.Vod.Repositories;

namespace Fluxy.Application.Vod.Services
{
    /// <summary>
    /// Reads the on-demand catalogue and changes it on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two rules decide most of what happens here, and they live in this class rather than at
    /// the call sites so that no second endpoint reaching the same action can forget them: a
    /// name is unique <i>within its category</i>, and a patch resolves "absent" against
    /// "empty" before anything is judged, so the rules see the row as it is about to become.
    /// </para>
    /// <para>
    /// Nothing here knows about HTTP. Which <see cref="VodAction"/> becomes a 201 and which a
    /// 409 is the transport layer's business.
    /// </para>
    /// </remarks>
    public sealed class VodService : IVodService
    {
        private readonly IVodRepository _items;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="VodService"/> class.
        /// </summary>
        /// <param name="items">Repository holding the items.</param>
        /// <param name="unitOfWork">Commits the changes one operation stages.</param>
        public VodService(IVodRepository items, IUnitOfWork unitOfWork)
        {
            _items = items;
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public Task<VodPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? categoryId,
            VodStatus? status,
            VodType? type,
            VodSortField sortBy,
            VodSortOrder sortOrder,
            CancellationToken cancellationToken = default)
            => _items.GetPageAsync(
                page,
                pageSize,
                search,
                categoryId,
                status,
                type,
                sortBy,
                sortOrder,
                cancellationToken);

        /// <inheritdoc />
        public Task<VodItem?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => _items.GetAsync(id, cancellationToken);

        /// <inheritdoc />
        public async Task<VodOutcome> CreateAsync(
            NewVodItem input,
            CancellationToken cancellationToken = default)
        {
            var name = input.Name.Trim();
            var source = input.Source.Trim();
            var subtitleUrl = VodRules.NullIfBlank(input.SubtitleUrl);
            var imdbId = VodRules.NullIfBlank(input.ImdbId);
            var posterUrl = VodRules.NullIfBlank(input.PosterUrl);
            var backdropUrl = VodRules.NullIfBlank(input.BackdropUrl);
            var plot = VodRules.NullIfBlank(input.Plot);
            var cast = VodRules.NormalizeList(input.Cast);
            var director = VodRules.NullIfBlank(input.Director);
            var genres = VodRules.NormalizeList(input.Genres);
            var countryCode = VodRules.NullIfBlank(input.CountryCode);

            var errors = VodRules.Validate(
                name,
                input.Status,
                input.Type,
                input.CategoryId,
                source,
                subtitleUrl,
                imdbId,
                posterUrl,
                backdropUrl,
                plot,
                cast,
                director,
                genres,
                input.ReleaseDate,
                input.Rating,
                countryCode);

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            // The date was checked by the rules above; parsed again here only to turn the text
            // into the value the row stores.
            VodRules.TryParseReleaseDate(input.ReleaseDate, out var releaseDate);

            // The category is checked after the shape rather than before it: a body that
            // already broke a rule of its own has nothing to say about the database yet.
            if (await _items.CategoryIsUsableAsync(input.CategoryId, cancellationToken) is false)
            {
                return Invalid(Field(nameof(NewVodItem.CategoryId), VodRules.CategoryRule));
            }

            if (await _items.IsNameTakenAsync(
                name,
                input.CategoryId,
                exceptItem: null,
                cancellationToken))
            {
                return AlreadyExists(nameof(NewVodItem.Name));
            }

            var itemId = await _items.AddAsync(
                BuildWrite(
                    name,
                    input.Status,
                    input.Type,
                    input.CategoryId,
                    source,
                    subtitleUrl,
                    imdbId,
                    posterUrl,
                    backdropUrl,
                    plot,
                    cast,
                    director,
                    genres,
                    releaseDate,
                    input.Rating,
                    countryCode),
                cancellationToken);

            if (await _unitOfWork.TrySaveAsync(cancellationToken) is { } createFailure)
            {
                // Only the name index may be answered as a name conflict. Any other unique
                // violation is a bug in this service, and dressing it up as a taken name
                // would hide it behind a sentence about something else entirely.
                return createFailure.IsConstraint(VodRules.NameIndexName)
                    ? AlreadyExists(nameof(NewVodItem.Name))
                    : throw createFailure;
            }

            return new VodOutcome
            {
                Action = VodAction.Created,
                VodId = itemId
            };
        }

        /// <inheritdoc />
        public async Task<VodOutcome> UpdateAsync(
            Guid id,
            VodItemPatch patch,
            CancellationToken cancellationToken = default)
        {
            var item = await _items.FindForUpdateAsync(id, cancellationToken);

            if (item is null)
            {
                return NotFound(id);
            }

            // Merged first and judged afterwards, so the rules below see the row as it is
            // about to become rather than as it was. Every "absent means leave it alone" of
            // the patch is resolved here, in one place.
            var name = patch.Name is null ? item.Name : patch.Name.Trim();
            var status = patch.Status ?? item.Status;
            var type = patch.Type ?? item.Type;
            var categoryId = patch.CategoryId ?? item.CategoryId;
            var source = patch.Source is null ? item.Source : patch.Source.Trim();
            var subtitleUrl = MergeText(patch.SubtitleUrl, item.SubtitleUrl);
            var imdbId = MergeText(patch.ImdbId, item.ImdbId);
            var posterUrl = MergeText(patch.PosterUrl, item.PosterUrl);
            var backdropUrl = MergeText(patch.BackdropUrl, item.BackdropUrl);
            var plot = MergeText(patch.Plot, item.Plot);
            var cast = patch.Cast is null ? item.Cast : VodRules.NormalizeList(patch.Cast);
            var director = MergeText(patch.Director, item.Director);
            var genres = patch.Genres is null ? item.Genres : VodRules.NormalizeList(patch.Genres);
            var countryCode = patch.CountryCode is null
                ? item.CountryCode
                : VodRules.NullIfBlank(patch.CountryCode);

            var releaseDate = item.ReleaseDate;

            if (patch.ReleaseDate is not null)
            {
                if (!VodRules.TryParseReleaseDate(patch.ReleaseDate, out var parsedDate))
                {
                    return Invalid(
                        Field(nameof(VodItemPatch.ReleaseDate), VodRules.ReleaseDateRule),
                        id);
                }

                releaseDate = parsedDate;
            }

            var rating = item.Rating;

            if (patch.Rating is not null)
            {
                if (!VodRules.TryParseRating(patch.Rating, out var parsedRating))
                {
                    return Invalid(Field(nameof(VodItemPatch.Rating), VodRules.RatingRule), id);
                }

                rating = parsedRating;
            }

            var errors = VodRules.Validate(
                name,
                status,
                type,
                categoryId,
                source,
                subtitleUrl,
                imdbId,
                posterUrl,
                backdropUrl,
                plot,
                cast,
                director,
                genres,
                patch.ReleaseDate,
                rating,
                countryCode);

            if (errors.Count > 0)
            {
                return Invalid(errors, id);
            }

            if (await _items.CategoryIsUsableAsync(categoryId, cancellationToken) is false)
            {
                return Invalid(Field(nameof(VodItemPatch.CategoryId), VodRules.CategoryRule), id);
            }

            if (await _items.IsNameTakenAsync(name, categoryId, id, cancellationToken))
            {
                return AlreadyExists(nameof(VodItemPatch.Name), id);
            }

            await _items.UpdateAsync(
                id,
                BuildWrite(
                    name,
                    status,
                    type,
                    categoryId,
                    source,
                    subtitleUrl,
                    imdbId,
                    posterUrl,
                    backdropUrl,
                    plot,
                    cast,
                    director,
                    genres,
                    releaseDate,
                    rating,
                    countryCode),
                cancellationToken);

            if (await _unitOfWork.TrySaveAsync(cancellationToken) is { } updateFailure)
            {
                return updateFailure.IsConstraint(VodRules.NameIndexName)
                    ? AlreadyExists(nameof(VodItemPatch.Name), id)
                    : throw updateFailure;
            }

            return new VodOutcome
            {
                Action = VodAction.Updated,
                VodId = id
            };
        }

        /// <inheritdoc />
        public async Task<VodOutcome> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            if (!await _items.RemoveAsync(id, cancellationToken))
            {
                return NotFound(id);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new VodOutcome
            {
                Action = VodAction.Removed,
                VodId = id
            };
        }

        /// <summary>Turns the merged values into the row the repository writes.</summary>
        private static VodItemWrite BuildWrite(
            string name,
            VodStatus status,
            VodType type,
            Guid categoryId,
            string source,
            string? subtitleUrl,
            string? imdbId,
            string? posterUrl,
            string? backdropUrl,
            string? plot,
            IReadOnlyList<string> cast,
            string? director,
            IReadOnlyList<string> genres,
            DateOnly? releaseDate,
            decimal? rating,
            string? countryCode)
            => new()
            {
                Name = name,
                Status = status,
                Type = type,
                CategoryId = categoryId,
                Source = source,
                SubtitleUrl = subtitleUrl,
                ImdbId = imdbId,
                PosterUrl = posterUrl,
                BackdropUrl = backdropUrl,
                Plot = plot,
                Cast = cast,
                Director = director,
                Genres = genres,
                ReleaseDate = releaseDate,
                Rating = rating,
                CountryCode = countryCode is null ? null : CountryCodes.Normalize(countryCode)
            };

        /// <summary>
        /// Resolves one nullable text field of a change: absent keeps, empty clears, anything
        /// else is trimmed and stored.
        /// </summary>
        private static string? MergeText(string? sent, string? stored)
            => sent is null ? stored : VodRules.NullIfBlank(sent);

        /// <summary>One field's worth of rejections, keyed by the C# property name.</summary>
        private static Dictionary<string, string[]> Field(string name, string[] sentences)
            => new(StringComparer.Ordinal) { [name] = sentences };

        private static VodOutcome NotFound(Guid id)
            => new() { Action = VodAction.NotFound, VodId = id };

        private static VodOutcome AlreadyExists(string field, Guid id = default)
            => new()
            {
                Action = VodAction.AlreadyExists,
                VodId = id,
                Errors = Field(field, ["Another video of this category already has that name."])
            };

        private static VodOutcome Invalid(
            IReadOnlyDictionary<string, string[]> errors,
            Guid id = default)
            => new() { Action = VodAction.Invalid, VodId = id, Errors = errors };
    }
}
