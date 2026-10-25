namespace Fluxy.API.Contracts.Series
{
    /// <summary>
    /// What a successful season creation reports.
    /// </summary>
    public sealed record SeasonCreatedResponse
    {
        /// <summary>Code reported once the season has been created.</summary>
        public required string Code { get; init; }

        /// <summary>Human-readable confirmation.</summary>
        public required string Message { get; init; }

        /// <summary>Series the episodes were filed under, as text.</summary>
        public required string SeriesId { get; init; }

        /// <summary>Season number every created episode carries.</summary>
        public required int SeasonNumber { get; init; }

        /// <summary>How many episode rows were created.</summary>
        public required int AddedCount { get; init; }

        /// <summary>Number of the first created episode, always one.</summary>
        public required int FirstEpisodeNumber { get; init; }

        /// <summary>Number of the last created episode.</summary>
        public required int LastEpisodeNumber { get; init; }
    }
}
