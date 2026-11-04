using Fluxy.Core.GeoIp.Utils;
using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.Utils
{
    /// <summary>
    /// The text a connection log query uses for its choices, and the member each spelling names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The table lives here so it can be tested where it is used.</b> The transport parses the
    /// query string and refuses what names nothing, but the mapping itself is a pure rule with no
    /// HTTP in it - and the unit test project references Core and Application only, never the API
    /// layer. Keeping the spelling table in the transport would leave every one of these rules
    /// reachable only through a running host, which is the wrong price for a table of strings.
    /// </para>
    /// <para>
    /// <b>Comparison is case-insensitive and whitespace-tolerant</b>, the same as every other list
    /// filter in this API: <c>kind=AUDIO</c> and <c>kind=audio</c> are one request. A value that
    /// names no member answers null, and the transport turns that into a refusal under the
    /// parameter's own name - never into "no filter", because a filter that quietly widened to
    /// everything would answer a request the caller did not make.
    /// </para>
    /// <para>
    /// The names run in both directions: <see cref="NameOf(ConnectionStreamKind)"/> is what the
    /// response carries, and it is the same spelling <see cref="ParseStreamKind"/> accepts, so a
    /// client never has to learn two vocabularies for one fact.
    /// </para>
    /// </remarks>
    public static class ConnectionLogQueryText
    {
        /// <summary>Turns <c>video</c> / <c>audio</c> into the medium it names, or null.</summary>
        /// <param name="value">Value from a query string.</param>
        public static ConnectionStreamKind? ParseStreamKind(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "video" => ConnectionStreamKind.VideoStream,
                "audio" => ConnectionStreamKind.AudioStream,
                _ => null
            };

        /// <summary>
        /// Turns <c>live</c> / <c>all</c> into whether only unfinished sessions are wanted, or
        /// null when the value names neither state.
        /// </summary>
        /// <param name="value">Value from a query string.</param>
        public static bool? ParseLiveOnly(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "live" => true,
                "all" => false,
                _ => null
            };

        /// <summary>Turns text into the column it names, or null when it names none.</summary>
        /// <param name="value">Value from a query string.</param>
        public static ConnectionLogSortField? ParseSortField(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "startedat" => ConnectionLogSortField.StartedAt,
                "endedat" => ConnectionLogSortField.EndedAt,
                _ => null
            };

        /// <summary>Turns <c>asc</c> / <c>desc</c> into the table's order.</summary>
        /// <param name="value">Value from a query string.</param>
        public static ConnectionLogSortOrder? ParseSortOrder(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "asc" => ConnectionLogSortOrder.Ascending,
                "desc" => ConnectionLogSortOrder.Descending,
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
        /// plainly means one country.
        /// </remarks>
        public static string? NormalizeCountry(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : CountryCodes.Normalize(value);

        /// <summary>The name a client reads a medium by: the spelling the filter accepts.</summary>
        /// <param name="kind">Medium to name.</param>
        public static string NameOf(ConnectionStreamKind kind) => kind switch
        {
            ConnectionStreamKind.VideoStream => "video",
            ConnectionStreamKind.AudioStream => "audio",
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "No name is defined for that stream kind.")
        };
    }
}
