namespace Fluxy.Core.Content.Commands
{
    /// <summary>
    /// What an administrator changes about an existing category. Every property is optional,
    /// and an absent one means "leave it alone".
    /// </summary>
    /// <remarks>
    /// The name is the only thing a category can be given that it does not already have: its
    /// type is the partition it lives in and never moves, and its position is not a field an
    /// individual request writes - the list is re-arranged wholesale, one reordering per type,
    /// rather than by moving one row and shifting the others.
    /// </remarks>
    public sealed record ContentCategoryPatch
    {
        /// <summary>New name, or null to keep the one on the row.</summary>
        public string? Name { get; init; }
    }

    /// <summary>
    /// The whole order of one type's categories, as the caller wants it to become.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The identifiers travel as the full ordered list of the type rather than as one moved
    /// row with its new position, for two reasons. First, one write of 1..N over the rows the
    /// request names is atomic where a per-row <c>UPDATE</c> of a moved row plus shifting the
    /// others is several - a request interrupted halfway through a shift would leave two
    /// categories sharing a position. Second, the caller has just dragged a row on a screen
    /// that shows the entire list: it already knows the whole order, and a request that named
    /// less than the whole of it would be a request the server has to interpret.
    /// </para>
    /// <para>
    /// The list has to name **every** category of the type, exactly once. A subset would be
    /// an order for some rows and silence for the rest, and a duplicate would be two claims
    /// about one row - both refused rather than guessed at.
    /// </para>
    /// </remarks>
}
