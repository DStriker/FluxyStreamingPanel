namespace Fluxy.API.Contracts.Series
{
    /// <summary>
    /// What the add-episode form sends to create one episode in a series.
    /// </summary>
    public sealed record CreateEpisodeRequest
    {
        /// <summary>Season the episode belongs to, counted from one.</summary>
        public int? SeasonNumber { get; init; }

        /// <summary>Number of the episode inside its season, counted from one.</summary>
        public int? EpisodeNumber { get; init; }

        /// <summary>URL or file path the episode is played from.</summary>
        public string? Source { get; init; }

        /// <summary>Whether it starts published, as the name of the enum member. Absent means inactive.</summary>
        public string? Status { get; init; }

        /// <summary>How it is delivered, as the name of the enum member: <c>relay</c> or <c>redirect</c>.</summary>
        public string? Type { get; init; }

        /// <summary>Display name, or none.</summary>
        public string? Name { get; init; }

        /// <summary>IMDb identifier, or none.</summary>
        public string? ImdbId { get; init; }

        /// <summary>Episode image address, or none.</summary>
        public string? ImageUrl { get; init; }

        /// <summary>Synopsis, or none.</summary>
        public string? Plot { get; init; }

        /// <summary>Release date written as <c>yyyy-MM-dd</c>, or none.</summary>
        public string? ReleaseDate { get; init; }

        /// <summary>Rating from 0 to 10, or none.</summary>
        public decimal? Rating { get; init; }
    }
}
