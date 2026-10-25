using Fluxy.Core.Series.Enums;

namespace Fluxy.Core.Series.Commands
{
    /// <summary>
    /// What an administrator changes about an existing episode. Every property is optional, and
    /// an absent one means "leave it alone".
    /// </summary>
    /// <remarks>
    /// The series identifier is deliberately absent: an episode is not moved between series by
    /// an edit, and the series it belongs to is the route the request was made under.
    /// </remarks>
    public sealed record EpisodePatch
    {
        /// <summary>New season number, or null to keep the one on the row.</summary>
        public int? SeasonNumber { get; init; }

        /// <summary>New episode number, or null to keep the one on the row.</summary>
        public int? EpisodeNumber { get; init; }

        /// <summary>New name; null keeps, empty clears.</summary>
        public string? Name { get; init; }

        /// <summary>New source; null keeps, empty is refused because a source is required.</summary>
        public string? Source { get; init; }

        /// <summary>New status, or null to keep the one on the row.</summary>
        public EpisodeStatus? Status { get; init; }

        /// <summary>New delivery kind, or null to keep the one on the row.</summary>
        public EpisodeType? Type { get; init; }

        /// <summary>New IMDb identifier; null keeps, empty clears.</summary>
        public string? ImdbId { get; init; }

        /// <summary>New image address; null keeps, empty clears.</summary>
        public string? ImageUrl { get; init; }

        /// <summary>New synopsis; null keeps, empty clears.</summary>
        public string? Plot { get; init; }

        /// <summary>New release date as <c>yyyy-MM-dd</c>; null keeps, empty clears.</summary>
        public string? ReleaseDate { get; init; }

        /// <summary>New rating as text; null keeps, empty clears.</summary>
        public string? Rating { get; init; }
    }
}
