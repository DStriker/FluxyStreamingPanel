using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Vod.Enums;
using Fluxy.Core.Vod.Models;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of one on-demand video. It is mutable, has no behaviour beyond
    /// mapping to <see cref="VodItem"/>, and knows nothing about HTTP.
    /// </summary>
    /// <remarks>
    /// The cast and the genres are PostgreSQL <c>text[]</c> columns rather than rows of their
    /// own. They are metadata read with the item and never queried by position, so an array
    /// keeps the order with none of the join, tracking and "unloaded collection reads as empty"
    /// machinery a child table would add.
    /// </remarks>
    public sealed class VodItemEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VodItemEntity"/> class for an item
        /// that is about to be inserted. The identifier and both audit timestamps are generated.
        /// </summary>
        public VodItemEntity()
        {
        }

        /// <summary>Display name, unique within <see cref="CategoryId"/>.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Whether the item is published or kept disabled.</summary>
        public VodStatus Status { get; set; }

        /// <summary>How the item is delivered: served here or redirected to.</summary>
        public VodType Type { get; set; }

        /// <summary>Category the item is filed under.</summary>
        public Guid CategoryId { get; set; }

        /// <summary>That category, read for the name the table column draws.</summary>
        public ContentCategoryEntity? Category { get; set; }

        /// <summary>URL or file path the video is played from.</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>Subtitles address, or null.</summary>
        public string? SubtitleUrl { get; set; }

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

        /// <summary>Upper case ISO 3166-1 alpha-2 country code, or null.</summary>
        public string? CountryCode { get; set; }

        /// <summary>Builds the business model out of this row.</summary>
        public VodItem ToModel() => new()
        {
            Id = Id,
            Name = Name,
            Status = Status,
            Type = Type,
            CategoryId = CategoryId,
            CategoryName = Category?.Name ?? string.Empty,
            Source = Source,
            SubtitleUrl = SubtitleUrl,
            ImdbId = ImdbId,
            PosterUrl = PosterUrl,
            BackdropUrl = BackdropUrl,
            Plot = Plot,
            Cast = Cast,
            Director = Director,
            Genres = Genres,
            ReleaseDate = ReleaseDate,
            Rating = Rating,
            CountryCode = CountryCode
        };
    }
}
