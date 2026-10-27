namespace Fluxy.Core.Content.Commands
{
    /// <summary>
    /// The whole order of the bouquets, as the caller wants it to become.
    /// </summary>
    /// <remarks>
    /// The identifiers travel as the full ordered list rather than as one moved row with its new
    /// position, for the reason <see cref="ReorderContentCategories"/> states: one write of
    /// 1..N is atomic where a per-row update plus a shift of the others is not, and the client
    /// has just dragged a row on a screen that shows the entire list. The list has to name
    /// **every** bouquet exactly once; a subset or a duplicate is refused rather than guessed at.
    /// </remarks>
    public sealed record ReorderBouquets
    {
        /// <summary>Every bouquet, in the order they should hold from now on.</summary>
        public required IReadOnlyList<Guid> OrderedIds { get; init; }
    }
}
