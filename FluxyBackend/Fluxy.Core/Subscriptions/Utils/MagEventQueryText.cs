using System.Globalization;
using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.Utils
{
    /// <summary>
    /// The text a MAG event query and body use for their choices, and the member each spelling
    /// names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The table lives here so it can be tested where it is used.</b> The unit test project
    /// references Core and Application only, never the API layer, so a mapping kept in the
    /// transport would be reachable only through a running host. The transport parses the text and
    /// refuses what names nothing; the mapping itself is a pure rule with no HTTP in it.
    /// </para>
    /// <para>
    /// <b>Comparison is case-insensitive and whitespace-tolerant</b>, the same as every other list
    /// filter in this API: <c>event=PLAYCHANNEL</c> and <c>event=playChannel</c> are one request.
    /// A value that names no member answers null, and the transport turns that into a refusal under
    /// the parameter's own name - never into "no filter".
    /// </para>
    /// <para>
    /// The names run in both directions: <see cref="NameOf(MagEventType)"/> and
    /// <see cref="NameOf(MagEventStatus)"/> are what the response carries, and the event spelling
    /// is the same one <see cref="ParseEventType"/> accepts, so a client never has to learn two
    /// vocabularies for one fact.
    /// </para>
    /// </remarks>
    public static class MagEventQueryText
    {
        /// <summary>
        /// The ISO 8601 spellings <see cref="ParseInstant"/> accepts. The offset is optional and,
        /// when absent, means UTC - the convention the other logs follow.
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

        /// <summary>
        /// Turns one of the six camelCase event names into the member it names, or null.
        /// </summary>
        /// <param name="value">Value from a query string or a body.</param>
        /// <remarks>
        /// Matching ignores case and surrounding whitespace, and the value is the camelCase name the
        /// response answers - never a number, because a number would let a client send a value the
        /// page would never print back.
        /// </remarks>
        public static MagEventType? ParseEventType(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "playchannel" => MagEventType.PlayChannel,
                "closeportal" => MagEventType.ClosePortal,
                "reloadportal" => MagEventType.ReloadPortal,
                "rebootdevice" => MagEventType.RebootDevice,
                "sendmessage" => MagEventType.SendMessage,
                "resetstblock" => MagEventType.ResetStbLock,
                _ => null
            };

        /// <summary>Turns text into the column it names, or null when it names none.</summary>
        /// <param name="value">Value from a query string.</param>
        public static MagEventSortField? ParseSortField(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "createdat" => MagEventSortField.CreatedAt,
                _ => null
            };

        /// <summary>Turns <c>asc</c> / <c>desc</c> into the table's order.</summary>
        /// <param name="value">Value from a query string.</param>
        public static MagEventSortOrder? ParseSortOrder(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "asc" => MagEventSortOrder.Asc,
                "desc" => MagEventSortOrder.Desc,
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
        /// <b>Parsed with the invariant culture, and an absent offset means UTC</b>, exactly like the
        /// subscription expiry: a value is a wire format, never the server's locale, so the same
        /// query means the same instant wherever the API runs. A present offset is honoured and the
        /// result normalized to UTC, because a caller that said <c>+03:00</c> meant that instant.
        /// </para>
        /// <para>
        /// A bare date is that day's midnight UTC, and a date-time may carry an offset, a fractional
        /// part and a lower-case <c>t</c>/<c>z</c>. Null is answered both for a blank value and for
        /// an unparseable one - the caller distinguishes them with <see cref="MagEventQuery.IsBlank"/>
        /// before treating null as "no filter".
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

        /// <summary>The name a client reads an operation by: the spelling the filter accepts.</summary>
        /// <param name="eventType">Operation to name.</param>
        public static string NameOf(MagEventType eventType) => eventType switch
        {
            MagEventType.PlayChannel => "playChannel",
            MagEventType.ClosePortal => "closePortal",
            MagEventType.ReloadPortal => "reloadPortal",
            MagEventType.RebootDevice => "rebootDevice",
            MagEventType.SendMessage => "sendMessage",
            MagEventType.ResetStbLock => "resetStbLock",
            _ => throw new ArgumentOutOfRangeException(
                nameof(eventType),
                eventType,
                "No name is defined for that event type.")
        };

        /// <summary>The name a client reads a confirmation state by.</summary>
        /// <param name="status">State to name.</param>
        public static string NameOf(MagEventStatus status) => status switch
        {
            MagEventStatus.NotConfirmed => "notConfirmed",
            MagEventStatus.Confirmed => "confirmed",
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "No name is defined for that event status.")
        };
    }
}
