namespace Fluxy.API.Contracts.Vod
{
    /// <summary>
    /// One row of the admin on-demand table: what a line of it draws.
    /// </summary>
    /// <remarks>
    /// The source is deliberately absent: playing a row goes through the endpoint that owns
    /// the delivery rule, so the table does not need the address. The detail endpoint answers
    /// with it when a form asks.
    /// </remarks>
    public sealed record AdminVodItemResponse
    {
        /// <summary>Identifier of the video, as text. The table shows it shortened.</summary>
        public required string Id { get; init; }

        /// <summary>Display name, unique within its category.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the video is published, as the name of the enum member.</summary>
        public required string Status { get; init; }

        /// <summary>How it is delivered, as the name of the enum member.</summary>
        public required string Type { get; init; }

        /// <summary>Identifier of the category, as text.</summary>
        public required string CategoryId { get; init; }

        /// <summary>Display name of that category, which is what the column draws.</summary>
        public required string CategoryName { get; init; }

        /// <summary>Portrait cover address, or null.</summary>
        public string? PosterUrl { get; init; }

        /// <summary>Release date as <c>yyyy-MM-dd</c>, or null.</summary>
        public string? ReleaseDate { get; init; }

        /// <summary>Rating from 0 to 10, or null.</summary>
        public decimal? Rating { get; init; }
    }
}
