namespace Fluxy.API.Contracts.Series
{
    /// <summary>
    /// What the add-series form sends to create a series in one request.
    /// </summary>
    /// <remarks>
    /// No data annotations: framework validation runs before the action, so a body that failed
    /// one would never reach the antiforgery check. Every field is checked in the service after
    /// the CSRF pair has been accepted. The status arrives as text so its parsing happens where
    /// a refusal can name the field a client sent.
    /// </remarks>
    public sealed record CreateSeriesRequest
    {
        /// <summary>Name of the series, as typed. Unique within its category.</summary>
        public string? Name { get; init; }

        /// <summary>Whether it starts published, as the name of the enum member. Absent means inactive.</summary>
        public string? Status { get; init; }

        /// <summary>Identifier of the category the series is filed under, as text.</summary>
        public string? CategoryId { get; init; }

        /// <summary>IMDb identifier, or none.</summary>
        public string? ImdbId { get; init; }

        /// <summary>Portrait cover address, or none.</summary>
        public string? PosterUrl { get; init; }

        /// <summary>Wide background picture address, or none.</summary>
        public string? BackdropUrl { get; init; }

        /// <summary>Synopsis, or none.</summary>
        public string? Plot { get; init; }

        /// <summary>Cast, in billing order.</summary>
        public IReadOnlyList<string>? Cast { get; init; }

        /// <summary>Director, or none.</summary>
        public string? Director { get; init; }

        /// <summary>Genres, in the order entered.</summary>
        public IReadOnlyList<string>? Genres { get; init; }

        /// <summary>Release date written as <c>yyyy-MM-dd</c>, or none.</summary>
        public string? ReleaseDate { get; init; }

        /// <summary>Rating from 0 to 10, or none.</summary>
        public decimal? Rating { get; init; }

        /// <summary>
        /// Bouquets the new series joins, as text; absent or empty means none.
        /// </summary>
        /// <remarks>
        /// A <b>set</b> rather than an order: the series is appended to the end of every bouquet
        /// it joins, and where it sits inside one belongs to the bouquet's own screen.
        /// </remarks>
        public IReadOnlyList<string>? BouquetIds { get; init; }
    }
}
