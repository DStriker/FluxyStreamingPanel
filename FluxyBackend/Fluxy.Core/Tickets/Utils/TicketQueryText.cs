using Fluxy.Core.Tickets.Enums;

namespace Fluxy.Core.Tickets.Utils
{
    /// <summary>
    /// The text a support ticket query uses for its choices, and the member each spelling names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The table lives here so it can be tested where it is used.</b> The unit test project
    /// references Core and Application only, never the API layer, so a mapping kept in the transport
    /// would be reachable only through a running host. The transport parses the query string and
    /// refuses what names nothing; the answer itself is a pure rule with no HTTP in it.
    /// </para>
    /// <para>
    /// <b>Comparison is case-insensitive and whitespace-tolerant</b>, the same as every other list
    /// filter in this API: <c>sortBy=LAstREPLYAT</c> and <c>sortBy=lastReplyAt</c> are one request.
    /// A value that names no member answers null, and the transport turns that into a refusal under
    /// the parameter's own name - never into "no filter".
    /// </para>
    /// </remarks>
    public static class TicketQueryText
    {
        /// <summary>Turns text into the column it names, or null when it names none.</summary>
        /// <param name="value">Value from a query string.</param>
        public static TicketSortField? ParseSortField(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "subject" => TicketSortField.Subject,
                "username" => TicketSortField.Username,
                "createdat" => TicketSortField.CreatedAt,
                "lastreplyat" => TicketSortField.LastReplyAt,
                _ => null
            };

        /// <summary>Turns <c>asc</c> / <c>desc</c> into the table's order.</summary>
        /// <param name="value">Value from a query string.</param>
        public static TicketSortOrder? ParseSortOrder(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "asc" => TicketSortOrder.Ascending,
                "desc" => TicketSortOrder.Descending,
                _ => null
            };

        /// <summary>
        /// Turns a status word into the word the query stores, or null when it names none.
        /// </summary>
        /// <param name="value">Value from a query string.</param>
        /// <remarks>
        /// The word is returned in the canonical lower-case spelling, and the set it is judged
        /// against is <see cref="TicketStatuses.All"/> - the same list a response row draws from, so
        /// a word a client may filter by and a word it may read back cannot drift apart.
        /// <c>unread</c> and <c>read</c> are relative to the viewer: the repository reads them
        /// against the unread flag of the side that is asking, while <c>closed</c> is the stored
        /// lifecycle and means the same thing to both sides.
        /// </remarks>
        public static string? ParseStatus(string? value)
        {
            if (!TicketStatuses.IsKnown(value))
            {
                return null;
            }

            return value!.Trim().ToLowerInvariant();
        }
    }
}
