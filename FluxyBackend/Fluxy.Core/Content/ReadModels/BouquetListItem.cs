namespace Fluxy.Core.Content.ReadModels
{
    /// <summary>
    /// One row of the bouquets table: the bouquet itself plus how much content it holds.
    /// </summary>
    /// <remarks>
    /// The counts are here rather than behind a second request because they are the fact the
    /// table's delete button has to tell the truth about: a bouquet that still holds content
    /// cannot be deleted, and a button disabled for no visible reason is a button that lies.
    /// The four kinds are counted apart because a bouquet holds each of them in its own order,
    /// and an operator who is about to look inside wants to know which kind the count is of.
    /// </remarks>
    public sealed record BouquetListItem
    {
        /// <summary>Identifier of the bouquet.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name of the bouquet.</summary>
        public required string Name { get; init; }

        /// <summary>Position of the bouquet among the bouquets.</summary>
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
}
