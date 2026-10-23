using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Outcomes
{
    /// <summary>
    /// The answer to one bulk operation over audio streams: the operation and one entry per
    /// stream it named.
    /// </summary>
    /// <remarks>
    /// A partial success is the normal shape. One stream of the selection can be missing while
    /// the others are changed, so the run as a whole does not fail and the report names which
    /// row was refused and why.
    /// </remarks>
    public sealed record BulkAudioStreamOutcome
    {
        /// <summary>The operation that was asked for.</summary>
        public required BulkAudioStreamAction Action { get; init; }

        /// <summary>One entry per identifier, in the order the request named them.</summary>
        public required IReadOnlyList<BulkAudioStreamItemResult> Items { get; init; }
    }
}
