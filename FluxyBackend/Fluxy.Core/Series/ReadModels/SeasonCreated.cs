namespace Fluxy.Core.Series.ReadModels
{
    /// <summary>
    /// What a successful season creation reports: which season, how many episodes were written,
    /// and the range their numbers cover.
    /// </summary>
    public sealed record SeasonCreated
    {
        /// <summary>Series the episodes were filed under.</summary>
        public required Guid SeriesId { get; init; }

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
