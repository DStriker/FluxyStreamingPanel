namespace Fluxy.API.Contracts.Series
{
    /// <summary>
    /// What the edit-series form sends. Every property is optional, and an absent one means
    /// "leave it alone".
    /// </summary>
    /// <remarks>
    /// A nullable text field answers "keep" and "clear" without a second field: <c>null</c>
    /// keeps whatever the row holds, an empty string clears it. <see cref="ReleaseDate"/> and
    /// <see cref="Rating"/> travel as text for the same reason. The lists follow the same shape
    /// with an extra state: an absent list keeps, an empty list clears, a list replaces.
    /// </remarks>
    public sealed record UpdateSeriesRequest
    {
        /// <summary>New name, or null to keep the one on the row.</summary>
        public string? Name { get; init; }

        /// <summary>New state, as the name of the enum member, or null to keep.</summary>
        public string? Status { get; init; }

        /// <summary>New category, as text; null keeps.</summary>
        public string? CategoryId { get; init; }

        /// <summary>New IMDb identifier; null keeps, empty clears.</summary>
        public string? ImdbId { get; init; }

        /// <summary>New poster address; null keeps, empty clears.</summary>
        public string? PosterUrl { get; init; }

        /// <summary>New backdrop address; null keeps, empty clears.</summary>
        public string? BackdropUrl { get; init; }

        /// <summary>New synopsis; null keeps, empty clears.</summary>
        public string? Plot { get; init; }

        /// <summary>New cast; null keeps, empty clears.</summary>
        public IReadOnlyList<string>? Cast { get; init; }

        /// <summary>New director; null keeps, empty clears.</summary>
        public string? Director { get; init; }

        /// <summary>New genres; null keeps, empty clears.</summary>
        public IReadOnlyList<string>? Genres { get; init; }

        /// <summary>New release date as <c>yyyy-MM-dd</c>; null keeps, empty clears.</summary>
        public string? ReleaseDate { get; init; }

        /// <summary>New rating as text; null keeps, empty clears.</summary>
        public string? Rating { get; init; }
    }
}
