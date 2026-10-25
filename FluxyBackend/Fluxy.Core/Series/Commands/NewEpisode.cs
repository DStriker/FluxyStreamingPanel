using Fluxy.Core.Series.Enums;

namespace Fluxy.Core.Series.Commands
{
    /// <summary>
    /// Everything an administrator supplies to create one episode by hand.
    /// </summary>
    public sealed record NewEpisode
    {
        /// <summary>Series the episode belongs to.</summary>
        public required Guid SeriesId { get; init; }

        /// <summary>Season the episode belongs to, counted from one.</summary>
        public required int SeasonNumber { get; init; }

        /// <summary>Number of the episode inside its season, counted from one.</summary>
        public required int EpisodeNumber { get; init; }

        /// <summary>Display name, or null.</summary>
        public string? Name { get; init; }

        /// <summary>URL or file path the episode is played from.</summary>
        public required string Source { get; init; }

        /// <summary>Whether the episode starts published or disabled.</summary>
        public required EpisodeStatus Status { get; init; }

        /// <summary>How the episode is delivered.</summary>
        public required EpisodeType Type { get; init; }

        /// <summary>IMDb identifier, or null.</summary>
        public string? ImdbId { get; init; }

        /// <summary>Address of the episode image, or null.</summary>
        public string? ImageUrl { get; init; }

        /// <summary>Synopsis, or null.</summary>
        public string? Plot { get; init; }

        /// <summary>Release date written as <c>yyyy-MM-dd</c>, or null.</summary>
        public string? ReleaseDate { get; init; }

        /// <summary>Rating from 0 to 10, or null.</summary>
        public decimal? Rating { get; init; }
    }
}
