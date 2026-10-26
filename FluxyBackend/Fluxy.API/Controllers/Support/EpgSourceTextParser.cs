using Fluxy.Core.Epg.Enums;

namespace Fluxy.API.Controllers.Support
{
    /// <summary>
    /// Turns the text of a guide source query parameter into the member it names.
    /// </summary>
    /// <remarks>
    /// Matched member by member rather than with enum binding: binding takes a member name
    /// spelled in full and would refuse the conventional lower-case one, before the action
    /// could run at all. Comparison happens after lowering, so <c>DaysKeep</c> is the same
    /// request as <c>dayskeep</c>.
    /// </remarks>
    internal static class EpgSourceTextParser
    {
        /// <summary>Turns text into the source column it names, or null when it names none.</summary>
        /// <param name="value">Value from a query string.</param>
        public static EpgSourceSortField? ParseSortField(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "name" => EpgSourceSortField.Name,
                "url" => EpgSourceSortField.Url,
                "dayskeep" => EpgSourceSortField.DaysKeep,
                "lastepgupdatedat" => EpgSourceSortField.LastEpgUpdatedAt,
                _ => null
            };

        /// <summary>Turns text into the channel column it names, or null when it names none.</summary>
        /// <param name="value">Value from a query string.</param>
        public static EpgSourceChannelSortField? ParseChannelSortField(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "streamname" => EpgSourceChannelSortField.StreamName,
                "channelkey" => EpgSourceChannelSortField.ChannelKey,
                _ => null
            };

        /// <summary>Turns <c>asc</c> / <c>desc</c> into the source table's order.</summary>
        /// <param name="value">Value from a query string.</param>
        public static EpgSourceSortOrder? ParseSortOrder(string? value)
            => SortOrderParser.Parse(
                value,
                EpgSourceSortOrder.Ascending,
                EpgSourceSortOrder.Descending);

        /// <summary>Turns <c>asc</c> / <c>desc</c> into the channel table's order.</summary>
        /// <param name="value">Value from a query string.</param>
        public static EpgSourceChannelSortOrder? ParseChannelSortOrder(string? value)
            => SortOrderParser.Parse(
                value,
                EpgSourceChannelSortOrder.Ascending,
                EpgSourceChannelSortOrder.Descending);
    }
}
