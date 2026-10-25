using Fluxy.Core.Series.Enums;

namespace Fluxy.Core.Series.Models
{
    /// <summary>
    /// Immutable business representation of one episode: the address it is played from, its
    /// place in the series, and the metadata the catalogue shows around it.
    /// </summary>
    /// <remarks>
    /// The pair <see cref="SeasonNumber"/> and <see cref="EpisodeNumber"/> is the episode's
    /// identity inside its series. A whole series is one row of the series table and every
    /// episode is a row filed under it, so the number pair is unique within
    /// <see cref="SeriesId"/> by a database index rather than by the application remembering to
    /// check.
    /// </remarks>
    public sealed record Episode
    {
        /// <summary>Identifier of the episode.</summary>
        public required Guid Id { get; init; }

        /// <summary>Series the episode belongs to.</summary>
        public required Guid SeriesId { get; init; }

        /// <summary>Season the episode belongs to, counted from one.</summary>
        public required int SeasonNumber { get; init; }

        /// <summary>Number of the episode inside its season, counted from one.</summary>
        public required int EpisodeNumber { get; init; }

        /// <summary>Display name, or null when the operator recorded none.</summary>
        public string? Name { get; init; }

        /// <summary>URL or file path the episode is played from.</summary>
        public required string Source { get; init; }

        /// <summary>Whether the episode is offered to clients or kept disabled.</summary>
        public required EpisodeStatus Status { get; init; }

        /// <summary>How the episode is delivered: served by this installation or redirected to.</summary>
        public required EpisodeType Type { get; init; }

        /// <summary>IMDb identifier, or null when none is recorded.</summary>
        public string? ImdbId { get; init; }

        /// <summary>Address of the episode image, or null.</summary>
        public string? ImageUrl { get; init; }

        /// <summary>Synopsis, or null when none is recorded.</summary>
        public string? Plot { get; init; }

        /// <summary>Release date, or null when the episode does not record one.</summary>
        public DateOnly? ReleaseDate { get; init; }

        /// <summary>Rating from 0 to 10, or null when the episode is not rated.</summary>
        public decimal? Rating { get; init; }
    }
}
