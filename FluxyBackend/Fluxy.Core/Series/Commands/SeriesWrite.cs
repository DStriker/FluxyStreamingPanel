using Fluxy.Core.Series.Enums;

namespace Fluxy.Core.Series.Commands
{
    /// <summary>
    /// The values a create or an update writes to a series row.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Models.SeriesItem"/> because a write does not name the facts
    /// the storage owns - the identifier and the category's display name. Every optional value
    /// has already been resolved by the service: blanks arrived as null and the two lists were
    /// cleaned, so the repository writes exactly what it is given.
    /// </remarks>
    public sealed record SeriesWrite
    {
        /// <summary>Display name, unique within <see cref="CategoryId"/>.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the series is published or disabled.</summary>
        public required SeriesStatus Status { get; init; }

        /// <summary>Category the series is filed under.</summary>
        public required Guid CategoryId { get; init; }

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

        /// <summary>
        /// Bouquets the new series joins, or null for none. A <b>set</b> rather than an order:
        /// the series is appended to the end of every bouquet it joins.
        /// </summary>
        public IReadOnlyList<Guid>? BouquetIds { get; init; }
    }
}
