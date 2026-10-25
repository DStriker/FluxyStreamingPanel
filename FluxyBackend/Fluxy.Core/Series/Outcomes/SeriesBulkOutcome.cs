using Fluxy.Core.Series.Enums;

namespace Fluxy.Core.Series.Outcomes
{
    /// <summary>
    /// The whole of a bulk operation over series: what was asked for, and how each series ended.
    /// </summary>
    public sealed record SeriesBulkOutcome
    {
        /// <summary>The operation this answer is for, echoed so the answer describes itself.</summary>
        public required BulkSeriesAction Action { get; init; }

        /// <summary>One entry per identifier the request named, in the order it named them.</summary>
        public required IReadOnlyList<SeriesBulkItemResult> Items { get; init; }
    }
}
