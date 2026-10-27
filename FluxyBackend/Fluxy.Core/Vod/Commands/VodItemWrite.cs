using Fluxy.Core.Vod.Enums;

namespace Fluxy.Core.Vod.Commands
{
    /// <summary>
    /// The values a create or an update writes to an on-demand row.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Models.VodItem"/> because a write does not name the facts the
    /// storage owns - the identifier and the category's display name. Every optional value
    /// has already been resolved by the service: blanks arrived as null and the two lists were
    /// cleaned, so the repository writes exactly what it is given.
    /// </remarks>
    public sealed record VodItemWrite
    {
        /// <summary>Display name, unique within <see cref="CategoryId"/>.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the item is published or disabled.</summary>
        public required VodStatus Status { get; init; }

        /// <summary>How the item is delivered.</summary>
        public required VodType Type { get; init; }

        /// <summary>Category the item is filed under.</summary>
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

        /// <summary>Cast, in billing order; empty when none.</summary>
        public required IReadOnlyList<string> Cast { get; init; }

        /// <summary>Director, or null.</summary>
        public string? Director { get; init; }

        /// <summary>Genres, in the order entered; empty when none.</summary>
        public required IReadOnlyList<string> Genres { get; init; }

        /// <summary>Release date, or null.</summary>
        public DateOnly? ReleaseDate { get; init; }

        /// <summary>Rating from 0 to 10, or null.</summary>
        public decimal? Rating { get; init; }

        /// <summary>Upper case ISO 3166-1 alpha-2 country code, or null.</summary>
        public string? CountryCode { get; init; }

        /// <summary>
        /// Bouquets the new video joins, or null for none. A <b>set</b> rather than an order:
        /// the video is appended to the end of every bouquet it joins.
        /// </summary>
        public IReadOnlyList<Guid>? BouquetIds { get; init; }
    }
}
