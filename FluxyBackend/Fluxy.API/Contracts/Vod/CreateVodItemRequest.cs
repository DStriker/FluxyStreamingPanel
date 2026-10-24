namespace Fluxy.API.Contracts.Vod
{
    /// <summary>
    /// What the add-video form sends to create an on-demand item in one request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No data annotations on purpose: framework validation runs before the action, so a body
    /// that failed one would never reach the antiforgery check. Every field is checked in the
    /// service after the CSRF pair has been accepted.
    /// </para>
    /// <para>
    /// The kind and the state arrive as text so their parsing happens where a refusal can name
    /// the field a client sent. Metadata is optional; a blank value is stored as "none".
    /// </para>
    /// </remarks>
    public sealed record CreateVodItemRequest
    {
        /// <summary>Name of the video, as typed. Unique within its category.</summary>
        public string? Name { get; init; }

        /// <summary>How it is delivered, as the name of the enum member: <c>relay</c> or <c>redirect</c>.</summary>
        public string? Type { get; init; }

        /// <summary>
        /// Whether it starts published, as the name of the enum member. Absent means
        /// <c>inactive</c>.
        /// </summary>
        public string? Status { get; init; }

        /// <summary>Identifier of the category the video is filed under, as text.</summary>
        public string? CategoryId { get; init; }

        /// <summary>URL or file path the video is played from.</summary>
        public string? Source { get; init; }

        /// <summary>Subtitles address, or none.</summary>
        public string? SubtitleUrl { get; init; }

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

        /// <summary>Upper case ISO 3166-1 alpha-2 country code, or none.</summary>
        public string? CountryCode { get; init; }
    }
}
