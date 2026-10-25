using Fluxy.Core.Series.Enums;

namespace Fluxy.Core.Series.ReadModels
{
    /// <summary>
    /// One row of the episode table: what a line of it draws.
    /// </summary>
    /// <remarks>
    /// The source is present because the episode table is where an operator checks which file
    /// a row points at; there is no delivery endpoint in this pass whose address could hide it.
    /// </remarks>
    public sealed record EpisodeListItem
    {
        /// <summary>Identifier of the episode, as text.</summary>
        public required Guid Id { get; init; }

        /// <summary>Series the episode belongs to.</summary>
        public required Guid SeriesId { get; init; }

        /// <summary>Season the episode belongs to.</summary>
        public required int SeasonNumber { get; init; }

        /// <summary>Number of the episode inside its season.</summary>
        public required int EpisodeNumber { get; init; }

        /// <summary>Display name, or null.</summary>
        public string? Name { get; init; }

        /// <summary>Whether the episode is published or disabled.</summary>
        public required EpisodeStatus Status { get; init; }

        /// <summary>How the episode is delivered.</summary>
        public required EpisodeType Type { get; init; }

        /// <summary>URL or file path the episode is played from.</summary>
        public required string Source { get; init; }

        /// <summary>Address of the episode image, or null.</summary>
        public string? ImageUrl { get; init; }

        /// <summary>Release date, or null.</summary>
        public DateOnly? ReleaseDate { get; init; }

        /// <summary>Rating from 0 to 10, or null.</summary>
        public decimal? Rating { get; init; }
    }
}
