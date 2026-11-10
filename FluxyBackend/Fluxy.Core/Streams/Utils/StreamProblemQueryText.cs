using System.Globalization;
using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Utils
{
    /// <summary>
    /// The text a stream problem query uses for its choices, and the member each spelling names.
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
    /// filter in this API: <c>sortBy=CREATEDAT</c> and <c>sortBy=createdAt</c> are one request. A
    /// value that names no member answers null, and the transport turns that into a refusal under
    /// the parameter's own name - never into "no filter".
    /// </para>
    /// </remarks>
    public static class StreamProblemQueryText
    {
        /// <summary>
        /// The ISO 8601 spellings <see cref="ParseInstant"/> accepts. The offset is optional and,
        /// when absent, means UTC - the same wire convention the other logs follow.
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
        public static StreamProblemSortField? ParseSortField(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "createdat" => StreamProblemSortField.CreatedAt,
                _ => null
            };

        /// <summary>Turns <c>asc</c> / <c>desc</c> into the table's order.</summary>
        /// <param name="value">Value from a query string.</param>
        public static StreamProblemSortOrder? ParseSortOrder(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "asc" => StreamProblemSortOrder.Ascending,
                "desc" => StreamProblemSortOrder.Descending,
                _ => null
            };

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
        /// because a caller that said <c>+03:00</c> meant that instant. A bare date is that day's
        /// midnight UTC, a date-time may carry an offset and a fractional part, and a
        /// locale-dependent form such as <c>05/01/2026</c> names no single instant and is refused.
        /// </para>
        /// <para>
        /// Null is answered both for a blank value and for an unparseable one - the caller
        /// distinguishes them with <see cref="StreamProblemQuery.IsBlank"/> before treating null as
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
