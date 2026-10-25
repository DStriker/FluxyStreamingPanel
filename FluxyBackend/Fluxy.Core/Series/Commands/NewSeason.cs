using Fluxy.Core.Series.Enums;

namespace Fluxy.Core.Series.Commands
{
    /// <summary>
    /// Everything the add-season form supplies: which season, which folder to read, how the
    /// generated episode names are built, and the state and delivery kind every created
    /// episode starts with.
    /// </summary>
    /// <remarks>
    /// The folder is read on the server; a relative path is resolved against the content root
    /// the same way the on-demand player resolves a stored file. The folder path itself is an
    /// input rather than a stored field - each created episode stores its own full source.
    /// </remarks>
    public sealed record NewSeason
    {
        /// <summary>Series the season belongs to.</summary>
        public required Guid SeriesId { get; init; }

        /// <summary>Season number every created episode carries.</summary>
        public required int SeasonNumber { get; init; }

        /// <summary>Folder the episode files are read from.</summary>
        public required string FolderPath { get; init; }

        /// <summary>Whether the generated name carries the season number.</summary>
        public required bool AddSeasonNumberToName { get; init; }

        /// <summary>Whether the generated name carries the episode number.</summary>
        public required bool AddEpisodeNumberToName { get; init; }

        /// <summary>State every created episode starts with.</summary>
        public required EpisodeStatus Status { get; init; }

        /// <summary>Delivery kind every created episode starts with.</summary>
        public required EpisodeType Type { get; init; }
    }
}
