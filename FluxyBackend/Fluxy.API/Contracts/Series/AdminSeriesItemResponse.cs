namespace Fluxy.API.Contracts.Series
{
    /// <summary>
    /// One row of the admin series table: what a line of it draws.
    /// </summary>
    public sealed record AdminSeriesItemResponse
    {
        /// <summary>Identifier of the series, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Display name, unique within its category.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the series is published, as the name of the enum member.</summary>
        public required string Status { get; init; }

        /// <summary>Identifier of the category, as text.</summary>
        public required string CategoryId { get; init; }

        /// <summary>Display name of that category, which is what the column draws.</summary>
        public required string CategoryName { get; init; }

        /// <summary>Portrait cover address, or null.</summary>
        public string? PosterUrl { get; init; }

        /// <summary>The largest season number the series holds, or zero.</summary>
        public required int SeasonsCount { get; init; }

        /// <summary>How many episode rows the series holds.</summary>
        public required int EpisodesCount { get; init; }

        /// <summary>Release date as <c>yyyy-MM-dd</c>, or null.</summary>
        public string? ReleaseDate { get; init; }

        /// <summary>Rating from 0 to 10, or null.</summary>
        public decimal? Rating { get; init; }
    }
}
