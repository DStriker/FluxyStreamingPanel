using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Models;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of one series. It is mutable, has no behaviour beyond mapping to
    /// <see cref="SeriesItem"/>, and knows nothing about HTTP.
    /// </summary>
    /// <remarks>
    /// The cast and the genres are PostgreSQL <c>text[]</c> columns rather than rows of their
    /// own, for the reason the on-demand item gives: they are metadata read with the row and
    /// never queried by position, so an array keeps the order with none of the join and
    /// tracking machinery a child table would add. The episodes are rows of their own, because
    /// they are the resource this series exists to hold.
    /// </remarks>
    public sealed class SeriesEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SeriesEntity"/> class for a series that
        /// is about to be inserted. The identifier and both audit timestamps are generated.
        /// </summary>
        public SeriesEntity()
        {
        }

        /// <summary>Display name, unique within <see cref="CategoryId"/>.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Whether the series is published or kept disabled.</summary>
        public SeriesStatus Status { get; set; }

        /// <summary>Category the series is filed under.</summary>
        public Guid CategoryId { get; set; }

        /// <summary>That category, read for the name the table column draws.</summary>
        public ContentCategoryEntity? Category { get; set; }

        /// <summary>IMDb identifier, or null.</summary>
        public string? ImdbId { get; set; }

        /// <summary>Portrait cover address, or null.</summary>
        public string? PosterUrl { get; set; }

        /// <summary>Wide background picture address, or null.</summary>
        public string? BackdropUrl { get; set; }

        /// <summary>Synopsis, or null.</summary>
        public string? Plot { get; set; }

        /// <summary>Cast, in billing order.</summary>
        public string[] Cast { get; set; } = [];

        /// <summary>Director, or null.</summary>
        public string? Director { get; set; }

        /// <summary>Genres, in the order entered.</summary>
        public string[] Genres { get; set; } = [];

        /// <summary>Release date, or null.</summary>
        public DateOnly? ReleaseDate { get; set; }

        /// <summary>Rating from 0 to 10, or null.</summary>
        public decimal? Rating { get; set; }

        /// <summary>The episodes filed under this series, deleted with it.</summary>
        public ICollection<EpisodeEntity> Episodes { get; set; } = new List<EpisodeEntity>();

        /// <summary>Builds the business model out of this row.</summary>
        public SeriesItem ToModel() => new()
        {
            Id = Id,
            Name = Name,
            Status = Status,
            CategoryId = CategoryId,
            CategoryName = Category?.Name ?? string.Empty,
            ImdbId = ImdbId,
            PosterUrl = PosterUrl,
            BackdropUrl = BackdropUrl,
            Plot = Plot,
            Cast = Cast,
            Director = Director,
            Genres = Genres,
            ReleaseDate = ReleaseDate,
            Rating = Rating
        };
    }
}
