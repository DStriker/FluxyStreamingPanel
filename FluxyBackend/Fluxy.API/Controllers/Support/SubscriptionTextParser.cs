using System.Globalization;
using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.API.Controllers.Support
{
    /// <summary>
    /// Turns the text of a subscription query parameter or body field into the member it names.
    /// </summary>
    /// <remarks>
    /// Matched member by member rather than with enum binding: binding takes a member name spelled
    /// in full and would refuse the conventional lower-case one, before the action could run at
    /// all. Comparison happens after lowering, so <c>Expired</c> is the same request as
    /// <c>expired</c>.
    /// </remarks>
    internal static class SubscriptionTextParser
    {
        /// <summary>Turns text into the subscription column it names, or null when it names none.</summary>
        /// <param name="value">Value from a query string.</param>
        public static SubscriptionSortField? ParseSortField(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "createdat" => SubscriptionSortField.CreatedAt,
                "expireat" => SubscriptionSortField.ExpireAt,
                _ => null
            };

        /// <summary>Turns <c>asc</c> / <c>desc</c> into the subscription table's order.</summary>
        /// <param name="value">Value from a query string.</param>
        public static SubscriptionSortOrder? ParseSortOrder(string? value)
            => SortOrderParser.Parse(
                value,
                SubscriptionSortOrder.Ascending,
                SubscriptionSortOrder.Descending);

        /// <summary>Turns text into the computed state it names, or null when it names none.</summary>
        /// <param name="value">Value from a query string.</param>
        public static SubscriptionStatus? ParseStatus(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "active" => SubscriptionStatus.Active,
                "trial" => SubscriptionStatus.Trial,
                "blocked" => SubscriptionStatus.Blocked,
                "expired" => SubscriptionStatus.Expired,
                _ => null
            };

        /// <summary>
        /// Turns the names of the formats a body sent into the flag set they describe.
        /// </summary>
        /// <param name="values">Names from a body, possibly null.</param>
        /// <param name="formats">The flag set the names describe.</param>
        /// <returns>False when any name names no member, so the caller can refuse the field.</returns>
        /// <remarks>
        /// A name that names nothing refuses the whole list rather than being dropped: a client
        /// sending <c>["mpegTs","rtmp"]</c> believes it asked for two formats, and storing one
        /// would answer a request it did not make.
        /// </remarks>
        public static bool TryParseOutputFormats(
            IReadOnlyList<string>? values,
            out SubscriptionOutputFormat formats)
        {
            formats = SubscriptionOutputFormat.None;

            if (values is null)
            {
                return true;
            }

            foreach (var value in values)
            {
                var parsed = value?.Trim().ToLowerInvariant() switch
                {
                    "mpegts" or "mpeg-ts" => SubscriptionOutputFormat.MpegTs,
                    "hls" => SubscriptionOutputFormat.Hls,
                    _ => (SubscriptionOutputFormat?)null
                };

                if (parsed is not { } format)
                {
                    formats = SubscriptionOutputFormat.None;
                    return false;
                }

                formats |= format;
            }

            return true;
        }

        /// <summary>
        /// The names a client reads this flag set by. The inverse of
        /// <see cref="TryParseOutputFormats"/>, so the two cannot spell a format differently.
        /// </summary>
        /// <param name="formats">Formats to name.</param>
        public static IReadOnlyList<string> NameOf(SubscriptionOutputFormat formats)
        {
            var names = new List<string>(2);

            if (formats.HasFlag(SubscriptionOutputFormat.MpegTs))
            {
                names.Add("mpegTs");
            }

            if (formats.HasFlag(SubscriptionOutputFormat.Hls))
            {
                names.Add("hls");
            }

            return names;
        }

        /// <summary>
        /// The text a client reads a computed state by: the lower-case member name, the spelling
        /// the list filter accepts.
        /// </summary>
        /// <param name="status">State to name.</param>
        public static string NameOf(SubscriptionStatus status) => status switch
        {
            SubscriptionStatus.Active => "active",
            SubscriptionStatus.Trial => "trial",
            SubscriptionStatus.Blocked => "blocked",
            SubscriptionStatus.Expired => "expired",
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unknown subscription status.")
        };

        /// <summary>
        /// Reads a date an operator typed, or null when the value is blank or not a date.
        /// </summary>
        /// <param name="value">Value from a body.</param>
        /// <remarks>
        /// The invariant culture and no time, because an expiry is a calendar date: a bare date
        /// lands at midnight UTC, and the whole day is what the operator meant. A value that does
        /// not parse answers null, and the action refuses it under the field rather than storing a
        /// date it guessed.
        /// </remarks>
        public static DateTimeOffset? ParseExpireAt(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return DateTimeOffset.TryParse(
                value.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed)
                ? parsed
                : null;
        }
    }
}
