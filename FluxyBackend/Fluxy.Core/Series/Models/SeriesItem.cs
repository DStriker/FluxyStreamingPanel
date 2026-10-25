using Fluxy.Core.Series.Enums;

namespace Fluxy.Core.Series.Models
{
    /// <summary>
    /// Immutable business representation of one series: the catalogue row and the metadata an
    /// operator records about it. The episodes are their own model, filed under
    /// <see cref="Id"/>.
    /// </summary>
    /// <remarks>
    /// The number of seasons and the number of episodes are deliberately absent. Both are
    /// answers about the episodes stored beside the series rather than fields of a form, and a
    /// copy kept here would be a second source of truth for a fact the episode table already
    /// owns. They are read models, not columns.
    /// </remarks>
    public sealed record SeriesItem
    {
        /// <summary>Identifier of the series.</summary>
        public required Guid Id { get; init; }

        /// <summary>
        /// Display name, at most 200 characters, unique within <see cref="CategoryId"/> and
        /// compared case sensitively.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>Whether the series is published or kept disabled.</summary>
        public required SeriesStatus Status { get; init; }

        /// <summary>Category the series is filed under, always a category of series.</summary>
        public required Guid CategoryId { get; init; }

        /// <summary>Display name of that category, which is what the table column draws.</summary>
        public required string CategoryName { get; init; }

        /// <summary>IMDb identifier (<c>tt</c> and digits), or null when none is recorded.</summary>
        public string? ImdbId { get; init; }

        /// <summary>Address of the portrait cover, or null.</summary>
        public string? PosterUrl { get; init; }

        /// <summary>Address of the wide background picture, or null.</summary>
        public string? BackdropUrl { get; init; }

        /// <summary>Synopsis, or null when none is recorded.</summary>
        public string? Plot { get; init; }

        /// <summary>Cast, in billing order.</summary>
        public required IReadOnlyList<string> Cast { get; init; }

        /// <summary>Director, or null when none is recorded.</summary>
        public string? Director { get; init; }

        /// <summary>Genres, in the order the operator entered them.</summary>
        public required IReadOnlyList<string> Genres { get; init; }

        /// <summary>Release date, or null when the series does not record one.</summary>
        public DateOnly? ReleaseDate { get; init; }

        /// <summary>Rating from 0 to 10, or null when the series is not rated.</summary>
        public decimal? Rating { get; init; }
    }
}
