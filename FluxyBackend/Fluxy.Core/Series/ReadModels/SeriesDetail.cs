using Fluxy.Core.Series.Enums;

namespace Fluxy.Core.Series.ReadModels
{
    /// <summary>
    /// One series in full, for the edit form and the episode page's heading.
    /// </summary>
    /// <remarks>
    /// The two counts are the same projections the table draws, so the form can state how many
    /// seasons and episodes the series holds without a second request.
    /// </remarks>
    public sealed record SeriesDetail
    {
        /// <summary>Identifier of the series, as text.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name, unique within its category.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the series is published or disabled.</summary>
        public required SeriesStatus Status { get; init; }

        /// <summary>Identifier of the category, as text.</summary>
        public required Guid CategoryId { get; init; }

        /// <summary>Display name of that category.</summary>
        public required string CategoryName { get; init; }

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

        /// <summary>The largest season number the series holds, or zero.</summary>
        public required int SeasonsCount { get; init; }

        /// <summary>How many episode rows the series holds.</summary>
        public required int EpisodesCount { get; init; }
    }
}
