using Fluxy.Core.Servers.Enums;

namespace Fluxy.API.Controllers.Support
{
    /// <summary>
    /// Turns the text of a server query parameter into the member it names, and back.
    /// </summary>
    /// <remarks>
    /// Matched member by member rather than with enum binding or an ordered value: the status is
    /// two states of one server rather than a scale, and binding takes a member name spelled in
    /// full and would refuse the conventional lower-case one before the action could run.
    /// Comparison happens after lowering, so <c>clientCount</c> is the same request as
    /// <c>clientcount</c>.
    /// </remarks>
    internal static class ServerTextParser
    {
        /// <summary>Turns text into the column it names, or null when it names none.</summary>
        /// <param name="value">Value from a query string.</param>
        public static ServerSortField? ParseSortField(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "name" => ServerSortField.Name,
                "status" => ServerSortField.Status,
                "latency" => ServerSortField.Latency,
                "hostname" => ServerSortField.Hostname,
                "ip" => ServerSortField.Ip,
                "clientcount" => ServerSortField.ClientCount,
                "createdat" => ServerSortField.CreatedAt,
                _ => null
            };

        /// <summary>Turns <c>asc</c> / <c>desc</c> into the table's order.</summary>
        /// <param name="value">Value from a query string.</param>
        public static ServerSortOrder? ParseSortOrder(string? value)
            => SortOrderParser.Parse(
                value,
                ServerSortOrder.Ascending,
                ServerSortOrder.Descending);

        /// <summary>Turns text into the state it names, or null when it names none.</summary>
        /// <param name="value">Value from a query string or a JSON body.</param>
        public static ServerStatus? ParseStatus(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "disabled" => ServerStatus.Disabled,
                "active" => ServerStatus.Active,
                _ => null
            };

        /// <summary>The wire spelling of a stored state.</summary>
        /// <param name="status">Stored state.</param>
        public static string NameOf(ServerStatus status) => status.ToString();
    }
}
