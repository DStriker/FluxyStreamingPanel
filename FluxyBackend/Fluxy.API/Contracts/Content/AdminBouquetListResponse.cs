namespace Fluxy.API.Contracts.Content
{
    /// <summary>
    /// One row of the bouquets table: the bouquet, its position, and how much it holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The four counts travel on every row because they are what the table's delete button has
    /// to tell the truth about: a bouquet that still holds content cannot be deleted, and a
    /// button disabled for no visible reason is a button that lies. A single total would answer
    /// "is it empty" and nothing else, while an operator looking at a package wants to know
    /// what kind of content is inside before opening it.
    /// </para>
    /// <para>
    /// <see cref="SortOrder"/> is the position the row holds, not the position it is shown at:
    /// the items arrive already ordered by it, so a client that draws them in order never needs
    /// to read it.
    /// </para>
    /// </remarks>
    public sealed record AdminBouquetItemResponse
    {
        /// <summary>Identifier of the bouquet, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Display name, unique for the installation without regard to case.</summary>
        public required string Name { get; init; }

        /// <summary>Position among the bouquets, lowest first.</summary>
        public required int SortOrder { get; init; }

        /// <summary>How many video streams the bouquet holds.</summary>
        public required int VideoStreamCount { get; init; }

        /// <summary>How many audio streams the bouquet holds.</summary>
        public required int AudioStreamCount { get; init; }

        /// <summary>How many on-demand videos the bouquet holds.</summary>
        public required int VideoOnDemandCount { get; init; }

        /// <summary>How many series the bouquet holds.</summary>
        public required int SeriesCount { get; init; }
    }

    /// <summary>
    /// Every bouquet, in the order an operator arranged them in.
    /// </summary>
    /// <remarks>
    /// Deliberately a list and not a page, for the reason the categories list gives: the
    /// bouquets are a screenful of packages rather than a per-row record, and a pager over a
    /// list that never outgrows one page would promise a second page that has no reason to
    /// exist.
    /// </remarks>
    public sealed record AdminBouquetListResponse
    {
        /// <summary>Every bouquet, lowest position first.</summary>
        public required IReadOnlyList<AdminBouquetItemResponse> Items { get; init; }
    }

    /// <summary>
    /// One content item as a bouquet screen names it.
    /// </summary>
    /// <remarks>
    /// The name and the category, and nothing else on purpose: the two lists of the content
    /// manager exist to be moved between, and almost every field beyond the name would be a
    /// column nobody reads in a list that can hold thousands of rows. The category is the one
    /// exception - it is what the filter over each list works on, so it is how a whole section
    /// is moved into a package in one gesture.
    /// </remarks>
    public sealed record BouquetMemberResponse
    {
        /// <summary>Identifier of the content item, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Display name of the content item.</summary>
        public required string Name { get; init; }

        /// <summary>Identifier of the category the item is filed under, as text.</summary>
        public required string CategoryId { get; init; }

        /// <summary>Display name of that category.</summary>
        public required string CategoryName { get; init; }
    }

    /// <summary>
    /// The two lists one tab of the bouquet content manager draws.
    /// </summary>
    /// <remarks>
    /// Both lists in one answer, because they are two halves of one sentence: a caller that
    /// fetched them separately could paint a list that had never existed - an item deleted
    /// between the two requests would be missing from both, and one added in between would
    /// appear in both at once.
    /// </remarks>
    public sealed record AdminBouquetContentResponse
    {
        /// <summary>
        /// What the bouquet holds of this kind, in the order it holds it. That order is the
        /// operator's, and it is what the write reads back.
        /// </summary>
        public required IReadOnlyList<BouquetMemberResponse> Members { get; init; }

        /// <summary>
        /// Everything of this kind the bouquet does not hold, ordered by name.
        /// </summary>
        public required IReadOnlyList<BouquetMemberResponse> Available { get; init; }
    }
}
