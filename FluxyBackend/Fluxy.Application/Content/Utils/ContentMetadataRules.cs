using System.Globalization;

namespace Fluxy.Application.Content.Utils
{
    /// <summary>
    /// The metadata rules the on-demand catalogue and the series catalogue share: the shape of
    /// an IMDb identifier, the two text forms a date and a rating arrive in, and the cleaning
    /// an ordered list of names gets before it is stored.
    /// </summary>
    /// <remarks>
    /// The two resources record the same metadata fields, so the parsing and normalisation live
    /// here once. What stays with each feature is its own sentence and its own include list:
    /// "another video of this category already has that name" and "another series of this
    /// category already has that name" are different things to tell an operator.
    /// </remarks>
    public static class ContentMetadataRules
    {
        /// <summary>Lowest rating an item may carry.</summary>
        public const decimal RatingMin = 0m;

        /// <summary>Highest rating an item may carry.</summary>
        public const decimal RatingMax = 10m;

        /// <summary>
        /// Trims every entry, drops blanks and repeats, and keeps the order the caller sent.
        /// </summary>
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

            if (parsed < RatingMin
                || parsed > RatingMax
                || decimal.Round(parsed, 1) != parsed)
            {
                return false;
            }

            value = parsed;
            return true;
        }

        /// <summary>Whether a value is <c>tt</c> followed by 5 to 10 digits.</summary>
        /// <param name="value">Value to judge.</param>
        public static bool IsImdbId(string value)
            => value.Length is >= 7 and <= 12
                && value.StartsWith("tt", StringComparison.Ordinal)
                && value[2..].All(char.IsAsciiDigit);

        /// <summary>Whether an optional text is short enough to store.</summary>
        /// <param name="value">Text to judge, possibly null or blank.</param>
        /// <param name="maxLength">Longest acceptable length.</param>
        public static bool Fits(string? value, int maxLength)
            => string.IsNullOrWhiteSpace(value) || value.Trim().Length <= maxLength;
    }
}
