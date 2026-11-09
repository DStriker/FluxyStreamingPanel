using System.Globalization;
using Fluxy.Core.GeoIp.Utils;
using Fluxy.Core.Users.Enums;

namespace Fluxy.Core.Users.Utils
{
    /// <summary>
    /// The text a user log query uses for its choices, and the member each spelling names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The table lives here so it can be tested where it is used.</b> The unit test project
    /// references Core and Application only, never the API layer, so a mapping kept in the
    /// transport would be reachable only through a running host. The transport parses the query
    /// string and refuses what names nothing; the answer itself is a pure rule with no HTTP in it.
    /// </para>
    /// <para>
    /// <b>Comparison is case-insensitive and whitespace-tolerant</b>, the same as every other list
    /// filter in this API: <c>sortBy=RECORDTYPE</c> and <c>sortBy=recordType</c> are one request.
    /// A value that names no member answers null, and the transport turns that into a refusal under
    /// the parameter's own name - never into "no filter".
    /// </para>
    /// </remarks>
    public static class UserLogQueryText
    {
        /// <summary>
        /// The ISO 8601 spellings <see cref="ParseInstant"/> accepts. The offset is optional and,
        /// when absent, means UTC - the same wire convention the credit and connection logs follow.
        /// </summary>
        private static readonly string[] InstantFormats =
        [
            "yyyy-MM-dd",
            "yyyy-MM-dd'T'HH:mm",
            "yyyy-MM-dd'T'HH:mmK",
            "yyyy-MM-dd'T'HH:mm:ss",
            "yyyy-MM-dd'T'HH:mm:ssK",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"
        ];

        /// <summary>Turns text into the column it names, or null when it names none.</summary>
        /// <param name="value">Value from a query string.</param>
        public static UserLogSortField? ParseSortField(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "createdat" => UserLogSortField.CreatedAt,
                "recordtype" => UserLogSortField.RecordType,
                _ => null
            };

        /// <summary>Turns <c>asc</c> / <c>desc</c> into the table's order.</summary>
        /// <param name="value">Value from a query string.</param>
        public static UserLogSortOrder? ParseSortOrder(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "asc" => UserLogSortOrder.Ascending,
                "desc" => UserLogSortOrder.Descending,
                _ => null
            };

        /// <summary>
        /// Canonical form of a country filter: upper-case ISO alpha-2, or null when the value is
        /// empty or names no country this installation knows.
        /// </summary>
        /// <param name="value">Value as typed.</param>
        /// <remarks>
        /// The stored column is canonical, so the filter has to be too: comparing a typed
        /// <c>de</c> against a stored <c>DE</c> would answer an empty page for a filter that
        /// plainly means one country. The connection log's country filter is the precedent, and
        /// this is deliberately its rule rather than a second opinion about the same column shape.
        /// </remarks>
        public static string? NormalizeCountry(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : CountryCodes.Normalize(value);

        /// <summary>
        /// Reads an optional creation-time bound: a blank value is "no bound", a value must be an
        /// ISO 8601 date or date-time.
        /// </summary>
        /// <param name="value">Value from a query string.</param>
        /// <returns>The instant the text names, or null when it is blank or not a date.</returns>
        /// <remarks>
        /// <para>
        /// <b>Parsed with the invariant culture, and an absent offset means UTC.</b> A value is a
        /// wire format, never the server's locale, so the same query means the same instant
        /// wherever the API runs. A present offset is honoured and the result normalized to UTC,
        /// because a caller that said <c>+03:00</c> meant that instant.
        /// </para>
        /// <para>
        /// Null is answered both for a blank value and for an unparseable one - the caller
        /// distinguishes them with <see cref="UserLogQuery.IsBlank"/> before treating null as
        /// "no filter".
        /// </para>
        /// </remarks>
        public static DateTimeOffset? ParseInstant(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            // ISO 8601's separators may be spelled in either case. Normalizing them is what makes
            // `2026-05-01t10:00:00z` the same request as `2026-05-01T10:00:00Z`, while the format
            // list below still refuses anything that is not a date.
            var text = value
                .Trim()
                .Replace('t', 'T')
                .Replace('z', 'Z');

            return DateTimeOffset.TryParseExact(
                text,
                InstantFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed)
                ? parsed
                : null;
        }
    }
}
