using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Models;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of one episode of a series. It is mutable, has no behaviour
    /// beyond mapping to <see cref="Episode"/>, and knows nothing about HTTP.
    /// </summary>
    public sealed class EpisodeEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EpisodeEntity"/> class for an episode
        /// that is about to be inserted. The identifier and both audit timestamps are generated.
        /// </summary>
        public EpisodeEntity()
        {
        }

        /// <summary>Series the episode belongs to. Deleted with the series.</summary>
        public Guid SeriesId { get; set; }

        /// <summary>That series.</summary>
        public SeriesEntity? Series { get; set; }

        /// <summary>Season the episode belongs to, counted from one.</summary>
        public int SeasonNumber { get; set; }

        /// <summary>Number of the episode inside its season, counted from one.</summary>
        public int EpisodeNumber { get; set; }

        /// <summary>Display name, or null.</summary>
        public string? Name { get; set; }

        /// <summary>URL or file path the episode is played from.</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>Whether the episode is published or kept disabled.</summary>
        public EpisodeStatus Status { get; set; }

        /// <summary>How the episode is delivered.</summary>
        public EpisodeType Type { get; set; }

        /// <summary>IMDb identifier, or null.</summary>
        public string? ImdbId { get; set; }

        /// <summary>Address of the episode image, or null.</summary>
        public string? ImageUrl { get; set; }

        /// <summary>Synopsis, or null.</summary>
        public string? Plot { get; set; }

        /// <summary>Release date, or null.</summary>
        public DateOnly? ReleaseDate { get; set; }

        /// <summary>Rating from 0 to 10, or null.</summary>
        public decimal? Rating { get; set; }

        /// <summary>Builds the business model out of this row.</summary>
        public Episode ToModel() => new()
        {
            Id = Id,
            SeriesId = SeriesId,
            SeasonNumber = SeasonNumber,
            EpisodeNumber = EpisodeNumber,
            Name = Name,
            Source = Source,
            Status = Status,
            Type = Type,
            ImdbId = ImdbId,
            ImageUrl = ImageUrl,
            Plot = Plot,
            ReleaseDate = ReleaseDate,
            Rating = Rating
        };
    }
}
