using Fluxy.Core.Series.Enums;

namespace Fluxy.Core.Series.Commands
{
    /// <summary>
    /// Everything an administrator supplies to create a series in one request.
    /// </summary>
    /// <remarks>
    /// The metadata fields are optional and a blank one is stored as "none". The status is a
    /// field of the create: an operator decides when adding the series whether it is published
    /// straight away, and the endpoint answers <see cref="SeriesStatus.Inactive"/> when the
    /// body omits it.
    /// </remarks>
    public sealed record NewSeries
    {
        /// <summary>Name the series gets. Trimmed, compared case sensitively.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the series starts published or disabled.</summary>
        public required SeriesStatus Status { get; init; }

        /// <summary>Category the series is filed under. Must be a category of series.</summary>
        public required Guid CategoryId { get; init; }

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
    }
}
