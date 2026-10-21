using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Outcomes
{
    /// <summary>
    /// The whole of a bulk operation: what was asked for, and how each stream ended.
    /// </summary>
    /// <remarks>
    /// Partial success is the shape, for the reason <c>BulkUserOutcome</c> gives: the streams
    /// of one page are independent rows, and one row holding a name the destination category
    /// already uses is a fact about that row and not about the other nineteen. There is no
    /// status and no success flag here, because those are facts about transport.
    /// </remarks>
    public sealed record BulkVideoStreamOutcome
    {
        /// <summary>The operation this answer is for, echoed so the answer describes itself.</summary>
        public required BulkVideoStreamAction Action { get; init; }

        /// <summary>One entry per identifier the request named, in the order it named them.</summary>
        public required IReadOnlyList<BulkVideoStreamItemResult> Items { get; init; }
    }
}
