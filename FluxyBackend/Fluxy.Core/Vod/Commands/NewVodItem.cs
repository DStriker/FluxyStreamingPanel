using Fluxy.Core.Vod.Enums;

namespace Fluxy.Core.Vod.Commands
{
    /// <summary>
    /// Everything an administrator supplies to create an on-demand item in one request.
    /// </summary>
    /// <remarks>
    /// Metadata fields are optional and a blank one is stored as "none". <see cref="Status"/>
    /// is a field of the create: an operator decides when adding the item whether it is
    /// published straight away, and the endpoint answers <see cref="VodStatus.Inactive"/> when
    /// the body omits it.
    /// </remarks>
    public sealed record NewVodItem
    {
        /// <summary>Name the item gets. Trimmed, compared case sensitively.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the item starts published or disabled.</summary>
        public required VodStatus Status { get; init; }

        /// <summary>How the item is delivered.</summary>
        public required VodType Type { get; init; }

        /// <summary>Category the item is filed under. Must be a category of on-demand video.</summary>
        public required Guid CategoryId { get; init; }

        /// <summary>URL or file path the video is played from.</summary>
        public required string Source { get; init; }

        /// <summary>Subtitles address, or null.</summary>
        public string? SubtitleUrl { get; init; }

        /// <summary>IMDb identifier, or null.</summary>
        public string? ImdbId { get; init; }

        /// <summary>Portrait cover address, or null.</summary>
        public string? PosterUrl { get; init; }

        /// <summary>Wide background picture address, or null.</summary>
        public string? BackdropUrl { get; init; }

        /// <summary>Synopsis, or null.</summary>
        public string? Plot { get; init; }

        /// <summary>Cast, in billing order.</summary>
        public IReadOnlyList<string> Cast { get; init; } = [];

        /// <summary>Director, or null.</summary>
        public string? Director { get; init; }

        /// <summary>Genres, in the order entered.</summary>
        public IReadOnlyList<string> Genres { get; init; } = [];

        /// <summary>Release date written as <c>yyyy-MM-dd</c>, or null.</summary>
        public string? ReleaseDate { get; init; }

        /// <summary>Rating from 0 to 10, or null.</summary>
        public decimal? Rating { get; init; }

        /// <summary>Upper case ISO 3166-1 alpha-2 country code, or null.</summary>
        public string? CountryCode { get; init; }
    }
}
