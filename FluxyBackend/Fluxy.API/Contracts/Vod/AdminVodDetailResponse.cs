namespace Fluxy.API.Contracts.Vod
{
    /// <summary>
    /// One on-demand video in full, for the edit form.
    /// </summary>
    /// <remarks>
    /// The date is a plain <c>yyyy-MM-dd</c> string rather than a timestamp: a release date
    /// has no time of day, and sending one would invite a time zone question with no correct
    /// answer. The two lists arrive in the order the operator entered them.
    /// </remarks>
    public sealed record AdminVodDetailResponse
    {
        /// <summary>Identifier of the video, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Display name, unique within its category.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the video is published, as the name of the enum member.</summary>
        public required string Status { get; init; }

        /// <summary>How it is delivered, as the name of the enum member.</summary>
        public required string Type { get; init; }

        /// <summary>Identifier of the category, as text.</summary>
        public required string CategoryId { get; init; }

        /// <summary>Display name of that category.</summary>
        public required string CategoryName { get; init; }

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
        public required IReadOnlyList<string> Cast { get; init; }

        /// <summary>Director, or null.</summary>
        public string? Director { get; init; }

        /// <summary>Genres, in the order entered.</summary>
        public required IReadOnlyList<string> Genres { get; init; }

        /// <summary>Release date as <c>yyyy-MM-dd</c>, or null.</summary>
        public string? ReleaseDate { get; init; }

        /// <summary>Rating from 0 to 10, or null.</summary>
        public decimal? Rating { get; init; }

        /// <summary>Upper case ISO 3166-1 alpha-2 country code, or null.</summary>
        public string? CountryCode { get; init; }

        /// <summary>
        /// Bouquets the video belongs to, as text, in the bouquets' own order. A <b>set</b>:
        /// it answers "which packages is this video in" for the form's picker.
        /// </summary>
        public required IReadOnlyList<string> BouquetIds { get; init; }
    }
}
