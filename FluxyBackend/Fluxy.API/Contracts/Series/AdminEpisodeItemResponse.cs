namespace Fluxy.API.Contracts.Series
{
    /// <summary>
    /// One row of the admin episode table.
    /// </summary>
    public sealed record AdminEpisodeItemResponse
    {
        /// <summary>Identifier of the episode, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Series the episode belongs to, as text.</summary>
        public required string SeriesId { get; init; }

        /// <summary>Season the episode belongs to.</summary>
        public required int SeasonNumber { get; init; }

        /// <summary>Number of the episode inside its season.</summary>
        public required int EpisodeNumber { get; init; }

        /// <summary>Display name, or null.</summary>
        public string? Name { get; init; }

        /// <summary>Whether the episode is published, as the name of the enum member.</summary>
        public required string Status { get; init; }

        /// <summary>How the episode is delivered, as the name of the enum member.</summary>
        public required string Type { get; init; }

        /// <summary>URL or file path the episode is played from.</summary>
        public required string Source { get; init; }

        /// <summary>Address of the episode image, or null.</summary>
        public string? ImageUrl { get; init; }

        /// <summary>Release date as <c>yyyy-MM-dd</c>, or null.</summary>
        public string? ReleaseDate { get; init; }

        /// <summary>Rating from 0 to 10, or null.</summary>
        public decimal? Rating { get; init; }
    }
}
