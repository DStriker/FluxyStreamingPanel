using Fluxy.Core.Vod.Enums;

namespace Fluxy.Core.Vod.ReadModels
{
    /// <summary>
    /// One row of the on-demand table: what a line of it draws, and nothing it does not.
    /// </summary>
    /// <remarks>
    /// The source is deliberately absent. Playing a row goes through the endpoint that owns
    /// the delivery rule, so the table does not need the address and a page of twenty items
    /// does not carry twenty long paths over the wire.
    /// </remarks>
    public sealed record VodListItem
    {
        /// <summary>Identifier of the item, as text. The table shows it shortened.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name, unique within its category.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the item is published or disabled.</summary>
        public required VodStatus Status { get; init; }

        /// <summary>How the item is delivered.</summary>
        public required VodType Type { get; init; }

        /// <summary>Category the item is filed under.</summary>
        public required Guid CategoryId { get; init; }

        /// <summary>Display name of that category, which is what the column draws.</summary>
        public required string CategoryName { get; init; }

        /// <summary>Address of the portrait cover, or null.</summary>
        public string? PosterUrl { get; init; }

        /// <summary>Release date, or null.</summary>
        public DateOnly? ReleaseDate { get; init; }

        /// <summary>Rating from 0 to 10, or null.</summary>
        public decimal? Rating { get; init; }
    }
}
