namespace Fluxy.Core.Content.ReadModels
{
    /// <summary>
    /// The two lists one tab of the bouquet content manager draws: what the bouquet holds of
    /// one content kind, in the order the operator arranged it, and what it does not hold.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both lists arrive in one answer on purpose. They are two halves of one sentence - "these
    /// are in, these are out" - and a caller that fetched them separately could paint a list
    /// that had never existed: an item deleted between the two requests would be missing from
    /// both, and an item added to the bouquet in between would appear in both at once. One
    /// request, one snapshot.
    /// </para>
    /// <para>
    /// Neither list is paged. The manager's job is to move items between two complete sets, and
    /// a pager over either half would make "select all" mean "select the page I am looking at"
    /// while reading as "select everything".
    /// </para>
    /// </remarks>
    public sealed record BouquetContent
    {
        /// <summary>
        /// What the bouquet holds of this kind, in the order it holds it (lowest position
        /// first). That order is the operator's, and it is what the request that writes the
        /// list back reads.
        /// </summary>
        public required IReadOnlyList<BouquetMember> Members { get; init; }

        /// <summary>
        /// Everything of this kind the bouquet does not hold, ordered by name. Content may
        /// belong to several bouquets at once, so an item that is missing here is missing here
        /// and nowhere else.
        /// </summary>
        public required IReadOnlyList<BouquetMember> Available { get; init; }
    }
}
