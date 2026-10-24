using Fluxy.Core.Vod.Enums;

namespace Fluxy.Core.Vod.Outcomes
{
    /// <summary>
    /// The whole of a bulk operation: what was asked for, and how each item ended.
    /// </summary>
    /// <remarks>
    /// Partial success is the shape: the items of one page are independent rows, and one row
    /// holding a name the destination category already uses is a fact about that row rather
    /// than about the other nineteen.
    /// </remarks>
    public sealed record BulkVodOutcome
    {
        /// <summary>The operation this answer is for, echoed so the answer describes itself.</summary>
        public required BulkVodAction Action { get; init; }

        /// <summary>One entry per identifier the request named, in the order it named them.</summary>
        public required IReadOnlyList<BulkVodItemResult> Items { get; init; }
    }
}
