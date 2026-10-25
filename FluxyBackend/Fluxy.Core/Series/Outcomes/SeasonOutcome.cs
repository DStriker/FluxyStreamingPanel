using Fluxy.Core.Series.Enums;

namespace Fluxy.Core.Series.Outcomes
{
    /// <summary>
    /// The outcome of one request to create a whole season from a folder.
    /// </summary>
    public sealed record SeasonOutcome
    {
        /// <summary>How the request ended.</summary>
        public required SeasonAction Action { get; init; }

        /// <summary>The series the season belongs to.</summary>
        public required Guid SeriesId { get; init; }

        /// <summary>The season the request named.</summary>
        public required int SeasonNumber { get; init; }

        /// <summary>How many episode rows were created; zero unless the action is Created.</summary>
        public required int AddedCount { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
