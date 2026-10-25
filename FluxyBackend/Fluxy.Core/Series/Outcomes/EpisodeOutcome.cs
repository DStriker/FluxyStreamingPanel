using Fluxy.Core.Series.Enums;

namespace Fluxy.Core.Series.Outcomes
{
    /// <summary>
    /// The outcome of one admin operation on an episode.
    /// </summary>
    public sealed record EpisodeOutcome
    {
        /// <summary>How the operation ended.</summary>
        public required EpisodeAction Action { get; init; }

        /// <summary>The series the episode belongs to.</summary>
        public required Guid SeriesId { get; init; }

        /// <summary>The episode the operation was about.</summary>
        public required Guid EpisodeId { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
