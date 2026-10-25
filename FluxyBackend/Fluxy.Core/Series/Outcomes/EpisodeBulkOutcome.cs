using Fluxy.Core.Series.Enums;

namespace Fluxy.Core.Series.Outcomes
{
    /// <summary>
    /// The whole of a bulk operation over episodes: what was asked for, and how each ended.
    /// </summary>
    public sealed record EpisodeBulkOutcome
    {
        /// <summary>The operation this answer is for, echoed so the answer describes itself.</summary>
        public required BulkEpisodeAction Action { get; init; }

        /// <summary>One entry per identifier the request named, in the order it named them.</summary>
        public required IReadOnlyList<EpisodeBulkItemResult> Items { get; init; }
    }
}
