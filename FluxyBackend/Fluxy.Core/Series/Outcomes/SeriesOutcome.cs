using Fluxy.Core.Series.Enums;

namespace Fluxy.Core.Series.Outcomes
{
    /// <summary>
    /// The outcome of one admin operation on a series.
    /// </summary>
    public sealed record SeriesOutcome
    {
        /// <summary>How the operation ended.</summary>
        public required SeriesAction Action { get; init; }

        /// <summary>The series the operation was about.</summary>
        public required Guid SeriesId { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
