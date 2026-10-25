namespace Fluxy.Core.Series.Utils
{
    /// <summary>
    /// Bounds of the values a series or an episode stores.
    /// </summary>
    /// <remarks>
    /// The limits live in this layer rather than beside the table configurations so a service
    /// can judge a request without referencing the data access assembly. The configurations
    /// use the same constants, so a column width and the rule that refuses an oversized value
    /// cannot drift apart.
    /// </remarks>
    public static class SeriesLimits
    {
        /// <summary>Longest display name a series may carry.</summary>
        public const int NameMaxLength = 200;

        /// <summary>Longest display name an episode may carry.</summary>
        public const int EpisodeNameMaxLength = 200;

        /// <summary>Longest source address: a URL or a path to a file.</summary>
        public const int SourceMaxLength = 2048;

        /// <summary>Longest metadata address (poster, backdrop or episode image).</summary>
        public const int ImageUrlMaxLength = 2048;

        /// <summary>Longest IMDb identifier.</summary>
        public const int ImdbIdMaxLength = 32;

        /// <summary>Longest synopsis kept in the text column.</summary>
        public const int PlotMaxLength = 4000;

        /// <summary>Longest director name.</summary>
        public const int DirectorMaxLength = 200;

        /// <summary>Most cast members one series records.</summary>
        public const int CastMaxItems = 20;

        /// <summary>Longest cast member name.</summary>
        public const int CastItemMaxLength = 100;

        /// <summary>Most genres one series records.</summary>
        public const int GenresMaxItems = 10;

        /// <summary>Longest genre name.</summary>
        public const int GenreItemMaxLength = 60;

        /// <summary>Lowest rating a series or an episode may carry.</summary>
        public const decimal RatingMin = 0m;

        /// <summary>Highest rating a series or an episode may carry.</summary>
        public const decimal RatingMax = 10m;

        /// <summary>Smallest season number.</summary>
        public const int SeasonNumberMin = 1;

        /// <summary>Largest season number.</summary>
        public const int SeasonNumberMax = 999;

        /// <summary>Smallest episode number.</summary>
        public const int EpisodeNumberMin = 1;

        /// <summary>Largest episode number.</summary>
        public const int EpisodeNumberMax = 9999;

        /// <summary>Longest folder path the add-season form accepts.</summary>
        public const int FolderPathMaxLength = 2048;

        /// <summary>Most video files one add-season request may turn into episodes.</summary>
        public const int MaxEpisodesPerSeason = 500;

        /// <summary>
        /// File extensions the add-season form treats as episodes. The list is deliberately
        /// finite: a folder of cover art and subtitles must not become a season of rows whose
        /// source no player could open.
        /// </summary>
        public static IReadOnlyList<string> EpisodeFileExtensions { get; } =
        [
            ".mp4",
            ".mkv",
            ".avi",
            ".mov",
            ".m4v",
            ".webm",
            ".ts",
            ".m2ts",
            ".mpg",
            ".mpeg",
            ".flv",
            ".wmv"
        ];
    }
}
