using Fluxy.Application.Content.Utils;
using Fluxy.Core.Series.Commands;
using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Utils;

namespace Fluxy.Application.Series.Utils
{
    /// <summary>
    /// The rules and sentences every write to a series shares.
    /// </summary>
    /// <remarks>
    /// A create and a patch reach the same row through different requests, so the rules that are
    /// a fact about the row rather than about the request live here once. The metadata parsing
    /// is the shared <see cref="ContentMetadataRules"/> the on-demand catalogue also uses.
    /// </remarks>
    public static class SeriesRules
    {
        /// <summary>The sentence a name that is blank or too long gets.</summary>
        public static readonly string[] NameRule =
        [
            "The name must be between 1 and " + SeriesLimits.NameMaxLength + " characters."
        ];

        /// <summary>The sentence a status that names no member gets.</summary>
        public static readonly string[] StatusRule =
            ["The status must be either active or inactive."];

        /// <summary>The sentence a category that names no row of the right kind gets.</summary>
        public static readonly string[] CategoryRule =
            ["The category must be an existing category of series."];

        /// <summary>The sentence a bouquet that names no row gets.</summary>
        public static readonly string[] BouquetsRule =
            ["Every bouquet must be an existing bouquet."];

        /// <summary>The sentence an IMDb identifier that is not one gets.</summary>
        public static readonly string[] ImdbRule =
            ["The IMDb identifier must be 'tt' followed by 5 to 10 digits."];

        /// <summary>The sentence an image address that is too long gets.</summary>
        public static readonly string[] ImageRule =
        [
            "The image address must be " + SeriesLimits.ImageUrlMaxLength +
                " characters or fewer."
        ];

        /// <summary>The sentence a synopsis that is too long gets.</summary>
        public static readonly string[] PlotRule =
            ["The synopsis must be " + SeriesLimits.PlotMaxLength + " characters or fewer."];

        /// <summary>The sentence a cast list that is too long or holds an oversized name gets.</summary>
        public static readonly string[] CastRule =
        [
            "Add at most " + SeriesLimits.CastMaxItems + " cast members of at most " +
                SeriesLimits.CastItemMaxLength + " characters each."
        ];

        /// <summary>The sentence a director name that is too long gets.</summary>
        public static readonly string[] DirectorRule =
            ["The director must be " + SeriesLimits.DirectorMaxLength + " characters or fewer."];

        /// <summary>The sentence a genre list that is too long or holds an oversized name gets.</summary>
        public static readonly string[] GenresRule =
        [
            "Add at most " + SeriesLimits.GenresMaxItems + " genres of at most " +
                SeriesLimits.GenreItemMaxLength + " characters each."
        ];

        /// <summary>The sentence a release date that is not <c>yyyy-MM-dd</c> gets.</summary>
        public static readonly string[] ReleaseDateRule =
            ["The release date must be written as yyyy-MM-dd."];

        /// <summary>The sentence a rating outside 0..10 or with more than one decimal gets.</summary>
        public static readonly string[] RatingRule =
            ["The rating must be between 0 and 10, with at most one decimal place."];

        /// <summary>The unique index making a series' name unique within its category.</summary>
        public const string NameIndexName = "IX_series_category_id_name";

        /// <summary>Whether a name is non-blank and short enough to store.</summary>
        /// <param name="name">Name to judge.</param>
        public static bool IsUsableName(string name)
            => name.Trim().Length is >= 1 and <= SeriesLimits.NameMaxLength;

        /// <summary>
        /// Checks everything about a series that can be judged without asking the database.
        /// </summary>
        /// <remarks>
        /// The keys are the C# property names of <see cref="NewSeries"/>, which are the same
        /// names the patch uses for these fields. The transport normalizes them to camelCase
        /// for the client. The category is checked here only for "no identifier at all";
        /// whether it exists is a question for the repository.
        /// </remarks>
        public static Dictionary<string, string[]> Validate(
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
            string? releaseDate,
            decimal? rating)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (!IsUsableName(name))
            {
                errors[nameof(NewSeries.Name)] = NameRule;
            }

            if (!Enum.IsDefined(status))
            {
                errors[nameof(NewSeries.Status)] = StatusRule;
            }

            if (categoryId == Guid.Empty)
            {
                errors[nameof(NewSeries.CategoryId)] = CategoryRule;
            }

            if (!string.IsNullOrWhiteSpace(imdbId)
                && (imdbId.Trim().Length > SeriesLimits.ImdbIdMaxLength
                    || !ContentMetadataRules.IsImdbId(imdbId.Trim())))
            {
                errors[nameof(NewSeries.ImdbId)] = ImdbRule;
            }

            if (!ContentMetadataRules.Fits(posterUrl, SeriesLimits.ImageUrlMaxLength))
            {
                errors[nameof(NewSeries.PosterUrl)] = ImageRule;
            }

            if (!ContentMetadataRules.Fits(backdropUrl, SeriesLimits.ImageUrlMaxLength))
            {
                errors[nameof(NewSeries.BackdropUrl)] = ImageRule;
            }

            if (!string.IsNullOrWhiteSpace(plot) && plot.Trim().Length > SeriesLimits.PlotMaxLength)
            {
                errors[nameof(NewSeries.Plot)] = PlotRule;
            }

            if (cast.Count > SeriesLimits.CastMaxItems
                || cast.Any(entry => string.IsNullOrWhiteSpace(entry)
                    || entry.Trim().Length > SeriesLimits.CastItemMaxLength))
            {
                errors[nameof(NewSeries.Cast)] = CastRule;
            }

            if (!ContentMetadataRules.Fits(director, SeriesLimits.DirectorMaxLength))
            {
                errors[nameof(NewSeries.Director)] = DirectorRule;
            }

            if (genres.Count > SeriesLimits.GenresMaxItems
                || genres.Any(entry => string.IsNullOrWhiteSpace(entry)
                    || entry.Trim().Length > SeriesLimits.GenreItemMaxLength))
            {
                errors[nameof(NewSeries.Genres)] = GenresRule;
            }

            if (!ContentMetadataRules.TryParseReleaseDate(releaseDate, out _))
            {
                errors[nameof(NewSeries.ReleaseDate)] = ReleaseDateRule;
            }

            if (rating is { } value
                && (value < SeriesLimits.RatingMin
                    || value > SeriesLimits.RatingMax
                    || decimal.Round(value, 1) != value))
            {
                errors[nameof(NewSeries.Rating)] = RatingRule;
            }

            return errors;
        }
    }
}
