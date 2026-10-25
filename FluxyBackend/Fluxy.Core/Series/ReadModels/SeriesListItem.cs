using Fluxy.Core.Series.Enums;

namespace Fluxy.Core.Series.ReadModels
{
    /// <summary>
    /// One row of the series table: what a line of it draws, and nothing it does not.
    /// </summary>
    /// <remarks>
    /// <see cref="SeasonsCount"/> and <see cref="EpisodesCount"/> are projections over the
    /// episode rows, not columns of the series table. The season count is the largest season
    /// number the series holds, so a series whose only episodes are season three reports three
    /// seasons - the number is the highest season, not the number of distinct seasons.
    /// </remarks>
    public sealed record SeriesListItem
    {
        /// <summary>Identifier of the series, as text. The table shows it shortened.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name, unique within its category.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the series is published or disabled.</summary>
        public required SeriesStatus Status { get; init; }

        /// <summary>Category the series is filed under.</summary>
        public required Guid CategoryId { get; init; }

        /// <summary>Display name of that category, which is what the column draws.</summary>
        public required string CategoryName { get; init; }

        /// <summary>Address of the portrait cover, or null.</summary>
        public string? PosterUrl { get; init; }

        /// <summary>The largest season number the series holds, or zero.</summary>
        public required int SeasonsCount { get; init; }

        /// <summary>How many episode rows the series holds.</summary>
        public required int EpisodesCount { get; init; }

        /// <summary>Release date, or null.</summary>
        public DateOnly? ReleaseDate { get; init; }

        /// <summary>Rating from 0 to 10, or null.</summary>
        public decimal? Rating { get; init; }
    }
}
