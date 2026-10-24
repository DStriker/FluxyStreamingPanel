using System.Globalization;
using Fluxy.Core.GeoIp.Utils;
using Fluxy.Core.Vod.Commands;
using Fluxy.Core.Vod.Enums;
using Fluxy.Core.Vod.Utils;

namespace Fluxy.Application.Vod.Utils
{
    /// <summary>
    /// The rules and sentences every write to an on-demand item shares.
    /// </summary>
    /// <remarks>
    /// A single create, a patch and a playlist import reach the same row through different
    /// requests, so the rules that are a fact about the row rather than about the request live
    /// here once. What is <i>not</i> here is what a request alone decides: a create has one
    /// name, an import has one name per entry, and a patch has "absent means keep". Those stay
    /// with the service that owns the shape of its own command.
    /// </remarks>
    public static class VodRules
    {
        /// <summary>The sentence a name that is blank or too long gets.</summary>
        public static readonly string[] NameRule =
        [
            "The name must be between 1 and " + VodLimits.NameMaxLength + " characters."
        ];

        /// <summary>The sentence a delivery kind that names no member gets.</summary>
        public static readonly string[] TypeRule =
            ["The type must be either relay or redirect."];

        /// <summary>The sentence a status that names no member gets.</summary>
        public static readonly string[] StatusRule =
            ["The status must be either active or inactive."];

        /// <summary>The sentence a category that names no row of the right kind gets.</summary>
        public static readonly string[] CategoryRule =
            ["The category must be an existing category of on-demand video."];

        /// <summary>The sentence a source that is blank or too long gets.</summary>
        public static readonly string[] SourceRule =
        [
            "The source must be between 1 and " + VodLimits.SourceMaxLength +
                " characters, either a URL or a path to a file."
        ];

        /// <summary>The sentence a subtitles address that is too long gets.</summary>
        public static readonly string[] SubtitleRule =
        [
            "The subtitles address must be " + VodLimits.SubtitleUrlMaxLength +
                " characters or fewer."
        ];

        /// <summary>The sentence an image address that is too long gets.</summary>
        public static readonly string[] ImageRule =
        [
            "The image address must be " + VodLimits.ImageUrlMaxLength +
                " characters or fewer."
        ];

        /// <summary>The sentence an IMDb identifier that is not one gets.</summary>
        public static readonly string[] ImdbRule =
            ["The IMDb identifier must be 'tt' followed by 5 to 10 digits."];

        /// <summary>The sentence a synopsis that is too long gets.</summary>
        public static readonly string[] PlotRule =
            ["The synopsis must be " + VodLimits.PlotMaxLength + " characters or fewer."];

        /// <summary>The sentence a director name that is too long gets.</summary>
        public static readonly string[] DirectorRule =
            ["The director must be " + VodLimits.DirectorMaxLength + " characters or fewer."];

        /// <summary>The sentence a cast list that is too long or holds an oversized name gets.</summary>
        public static readonly string[] CastRule =
        [
            "Add at most " + VodLimits.CastMaxItems + " cast members of at most " +
                VodLimits.CastItemMaxLength + " characters each."
        ];

        /// <summary>The sentence a genre list that is too long or holds an oversized name gets.</summary>
        public static readonly string[] GenresRule =
        [
            "Add at most " + VodLimits.GenresMaxItems + " genres of at most " +
                VodLimits.GenreItemMaxLength + " characters each."
        ];

        /// <summary>The sentence a release date that is not <c>yyyy-MM-dd</c> gets.</summary>
        public static readonly string[] ReleaseDateRule =
            ["The release date must be written as yyyy-MM-dd."];

        /// <summary>The sentence a rating outside 0..10 or with more than one decimal gets.</summary>
        public static readonly string[] RatingRule =
            ["The rating must be between 0 and 10, with at most one decimal place."];

        /// <summary>The sentence a country that is not a known code gets.</summary>
        public static readonly string[] CountryRule =
            ["The country must be a two-letter ISO code this installation knows."];

        /// <summary>The unique index making an item's name unique within its category.</summary>
        public const string NameIndexName = "IX_vod_items_category_id_name";

        /// <summary>Whether a name is non-blank and short enough to store.</summary>
        /// <param name="name">Name to judge.</param>
        public static bool IsUsableName(string name)
            => name.Trim().Length is >= 1 and <= VodLimits.NameMaxLength;

        /// <summary>Whether a source is non-blank and short enough to store.</summary>
        /// <param name="source">Source to judge.</param>
        public static bool IsUsableSource(string source)
            => source.Trim().Length is >= 1 and <= VodLimits.SourceMaxLength;

        /// <summary>
        /// Trims every entry, drops blanks and repeats, and keeps the order the caller sent.
        /// </summary>
        /// <remarks>
        /// Applied before validation so a list of blanks is judged as the empty list it is,
        /// and before the write so the array stores what the operator meant rather than what
        /// a text field happened to contain.
        /// </remarks>
        /// <param name="entries">Entries as the request sent them, possibly null.</param>
        public static IReadOnlyList<string> NormalizeList(IEnumerable<string>? entries)
        {
            if (entries is null)
            {
                return [];
            }

            var result = new List<string>();

            foreach (var entry in entries)
            {
                var trimmed = entry?.Trim();

                if (!string.IsNullOrEmpty(trimmed)
                    && !result.Contains(trimmed, StringComparer.Ordinal))
                {
                    result.Add(trimmed);
                }
            }

            return result;
        }

        /// <summary>Turns an empty string into null, so a blank field is stored as "none".</summary>
        /// <param name="value">Text a request sent, or null.</param>
        public static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        /// <summary>
        /// Reads a release date: blank means "none", anything else must be <c>yyyy-MM-dd</c>.
        /// </summary>
        /// <param name="text">Text the request sent.</param>
        /// <param name="value">The date, or null for a blank value.</param>
        /// <returns>False when the text is neither blank nor a date in that form.</returns>
        public static bool TryParseReleaseDate(string? text, out DateOnly? value)
        {
            value = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            if (DateOnly.TryParseExact(
                text.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
            {
                value = parsed;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reads a rating: blank means "none", anything else must be 0..10 with at most one
        /// decimal place.
        /// </summary>
        /// <param name="text">Text the request sent.</param>
        /// <param name="value">The rating, or null for a blank value.</param>
        /// <returns>False when the text is neither blank nor a rating in that range.</returns>
        public static bool TryParseRating(string? text, out decimal? value)
        {
            value = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            if (!decimal.TryParse(
                text.Trim(),
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var parsed))
            {
                return false;
            }

            if (parsed < VodLimits.RatingMin
                || parsed > VodLimits.RatingMax
                || decimal.Round(parsed, 1) != parsed)
            {
                return false;
            }

            value = parsed;
            return true;
        }

        /// <summary>
        /// Checks everything about an item that can be judged without asking the database.
        /// </summary>
        /// <remarks>
        /// The keys are the C# property names of <see cref="NewVodItem"/>, which are the same
        /// names the patch uses for these fields. The transport normalizes them to camelCase
        /// for the client. The category is checked here only for "no identifier at all";
        /// whether it exists is a question for the repository, asked after the body has proved
        /// it is worth asking about.
        /// </remarks>
        public static Dictionary<string, string[]> Validate(
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
            string? releaseDate,
            decimal? rating,
            string? countryCode)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (!IsUsableName(name))
            {
                errors[nameof(NewVodItem.Name)] = NameRule;
            }

            // A number outside the members would be a kind every reader has to guess about -
            // refused rather than coerced.
            if (!Enum.IsDefined(status))
            {
                errors[nameof(NewVodItem.Status)] = StatusRule;
            }

            if (!Enum.IsDefined(type))
            {
                errors[nameof(NewVodItem.Type)] = TypeRule;
            }

            if (categoryId == Guid.Empty)
            {
                errors[nameof(NewVodItem.CategoryId)] = CategoryRule;
            }

            if (!IsUsableSource(source))
            {
                errors[nameof(NewVodItem.Source)] = SourceRule;
            }

            if (!Fits(subtitleUrl, VodLimits.SubtitleUrlMaxLength))
            {
                errors[nameof(NewVodItem.SubtitleUrl)] = SubtitleRule;
            }

            if (!string.IsNullOrWhiteSpace(imdbId)
                && (imdbId.Trim().Length > VodLimits.ImdbIdMaxLength || !IsImdbId(imdbId.Trim())))
            {
                errors[nameof(NewVodItem.ImdbId)] = ImdbRule;
            }

            if (!Fits(posterUrl, VodLimits.ImageUrlMaxLength))
            {
                errors[nameof(NewVodItem.PosterUrl)] = ImageRule;
            }

            if (!Fits(backdropUrl, VodLimits.ImageUrlMaxLength))
            {
                errors[nameof(NewVodItem.BackdropUrl)] = ImageRule;
            }

            if (!string.IsNullOrWhiteSpace(plot) && plot.Trim().Length > VodLimits.PlotMaxLength)
            {
                errors[nameof(NewVodItem.Plot)] = PlotRule;
            }

            if (cast.Count > VodLimits.CastMaxItems
                || cast.Any(entry => string.IsNullOrWhiteSpace(entry)
                    || entry.Trim().Length > VodLimits.CastItemMaxLength))
            {
                errors[nameof(NewVodItem.Cast)] = CastRule;
            }

            if (!Fits(director, VodLimits.DirectorMaxLength))
            {
                errors[nameof(NewVodItem.Director)] = DirectorRule;
            }

            if (genres.Count > VodLimits.GenresMaxItems
                || genres.Any(entry => string.IsNullOrWhiteSpace(entry)
                    || entry.Trim().Length > VodLimits.GenreItemMaxLength))
            {
                errors[nameof(NewVodItem.Genres)] = GenresRule;
            }

            // The date itself has no invalid member; the rule is the text form it arrived in.
            // A date in the future is a legitimate announcement, so there is no range to
            // enforce.
            if (!TryParseReleaseDate(releaseDate, out _))
            {
                errors[nameof(NewVodItem.ReleaseDate)] = ReleaseDateRule;
            }

            if (rating is { } value
                && (value < VodLimits.RatingMin
                    || value > VodLimits.RatingMax
                    || decimal.Round(value, 1) != value))
            {
                errors[nameof(NewVodItem.Rating)] = RatingRule;
            }

            if (!string.IsNullOrWhiteSpace(countryCode)
                && CountryCodes.Normalize(countryCode) is null)
            {
                errors[nameof(NewVodItem.CountryCode)] = CountryRule;
            }

            return errors;
        }

        /// <summary>Whether an optional text is short enough to store.</summary>
        private static bool Fits(string? value, int maxLength)
            => string.IsNullOrWhiteSpace(value) || value.Trim().Length <= maxLength;

        /// <summary>Whether a value is <c>tt</c> followed by 5 to 10 digits.</summary>
        private static bool IsImdbId(string value)
            => value.Length is >= 7 and <= 12
                && value.StartsWith("tt", StringComparison.Ordinal)
                && value[2..].All(char.IsAsciiDigit);
    }
}
