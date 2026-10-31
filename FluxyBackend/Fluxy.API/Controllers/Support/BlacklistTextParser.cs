using Fluxy.Core.Blacklists.Enums;

namespace Fluxy.API.Controllers.Support
{
    /// <summary>
    /// Turns the text of a blacklist query parameter or body field into the member it names.
    /// </summary>
    /// <remarks>
    /// Matched member by member rather than with enum binding: binding takes a member name
    /// spelled in full and would refuse the conventional lower-case one, before the action
    /// could run at all. Comparison happens after lowering, so <c>BlockedAttempts</c> is the
    /// same request as <c>blockedattempts</c>, and the list names are the hyphenated spelling
    /// the specification uses (<c>user-agent</c>) beside the collapsed one, because both are
    /// what a caller would reasonably write.
    /// </remarks>
    internal static class BlacklistTextParser
    {
        /// <summary>Turns text into the list it names, or null when it names none.</summary>
        /// <param name="value">Value from a query string or a body.</param>
        public static BlacklistType? ParseType(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "ip" => BlacklistType.Ip,
                "isp" => BlacklistType.Isp,
                "user-agent" => BlacklistType.UserAgent,
                "useragent" => BlacklistType.UserAgent,
                "ua" => BlacklistType.UserAgent,
                _ => null
            };

        /// <summary>Turns text into the column it names, or null when it names none.</summary>
        /// <param name="value">Value from a query string.</param>
        public static BlacklistSortField? ParseSortField(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "value" => BlacklistSortField.Value,
                "blockedattempts" => BlacklistSortField.BlockedAttempts,
                "createdat" => BlacklistSortField.CreatedAt,
                _ => null
            };

        /// <summary>Turns <c>asc</c> / <c>desc</c> into the table's order.</summary>
        /// <param name="value">Value from a query string.</param>
        public static BlacklistSortOrder? ParseSortOrder(string? value)
            => SortOrderParser.Parse(
                value,
                BlacklistSortOrder.Ascending,
                BlacklistSortOrder.Descending);

        /// <summary>
        /// The name a list is reported under, which is the spelling the query parameter accepts
        /// as well - one name for one thing in both directions.
        /// </summary>
        /// <param name="type">List to name.</param>
        public static string NameOf(BlacklistType type) => type switch
        {
            BlacklistType.Ip => "ip",
            BlacklistType.Isp => "isp",
            BlacklistType.UserAgent => "user-agent",
            _ => throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "No name is defined for that blacklist.")
        };
    }
}
