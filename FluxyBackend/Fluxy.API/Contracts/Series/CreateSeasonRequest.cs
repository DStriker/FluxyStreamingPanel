namespace Fluxy.API.Contracts.Series
{
    /// <summary>
    /// What the add-season form sends: the season number, the folder to read, the two naming
    /// switches plus the state and delivery kind every created episode starts with.
    /// </summary>
    public sealed record CreateSeasonRequest
    {
        /// <summary>Season number every created episode carries.</summary>
        public int? SeasonNumber { get; init; }

        /// <summary>Folder the episode files are read from: a path on this server.</summary>
        public string? FolderPath { get; init; }

        /// <summary>Whether the generated name carries the season number.</summary>
        public bool? AddSeasonNumberToName { get; init; }

        /// <summary>Whether the generated name carries the episode number.</summary>
        public bool? AddEpisodeNumberToName { get; init; }

        /// <summary>State every created episode starts with, as text. Absent means inactive.</summary>
        public string? Status { get; init; }

        /// <summary>Delivery kind every created episode starts with, as text.</summary>
        public string? Type { get; init; }
    }
}
