using Fluxy.Core.Series.Enums;

namespace Fluxy.Core.Series.Outcomes
{
    /// <summary>
    /// How one series inside a bulk operation ended.
    /// </summary>
    public sealed record SeriesBulkItemResult
    {
        /// <summary>The series this entry is about.</summary>
        public required Guid SeriesId { get; init; }

        /// <summary>How the single-row operation ended for this series.</summary>
        public required SeriesAction Action { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
