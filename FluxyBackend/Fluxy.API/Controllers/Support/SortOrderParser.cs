namespace Fluxy.API.Controllers.Support
{
    /// <summary>
    /// Turns <c>asc</c> / <c>desc</c> into the order member of the resource's own enum.
    /// </summary>
    /// <remarks>
    /// The two words are read as plain text rather than bound to the enum, and the reason is
    /// the same on every list: enum binding takes a member name spelled in full, so
    /// <c>sortOrder=desc</c> would be refused while <c>sortOrder=Descending</c> was accepted -
    /// an API that answers a request this conventional with a 400 teaches its callers to spell
    /// things oddly. Each controller keeps its own one-line wrapper, so the call site still
    /// reads as this resource's order and only the comparison itself is shared.
    /// </remarks>
    internal static class SortOrderParser
    {
        /// <summary>
        /// The member the text names, or nothing at all when it names neither direction.
        /// </summary>
        /// <typeparam name="TOrder">Order enum of the resource being listed.</typeparam>
        /// <param name="value">Text the client sent.</param>
        /// <param name="ascending">Member <c>asc</c> means.</param>
        /// <param name="descending">Member <c>desc</c> means.</param>
        public static TOrder? Parse<TOrder>(string? value, TOrder ascending, TOrder descending)
            where TOrder : struct, Enum
            => value?.Trim().ToLowerInvariant() switch
            {
                "asc" => ascending,
                "desc" => descending,
                _ => null
            };
    }
}
