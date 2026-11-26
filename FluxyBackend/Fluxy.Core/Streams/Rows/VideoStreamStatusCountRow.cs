using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Rows
{
    /// <summary>
    /// How many video streams one stored status holds.
    /// </summary>
    /// <remarks>
    /// A projection shaped for one query: the dashboard turns the two groups into "running" and
    /// "stopped" counts. A status no stream holds is absent from the answer rather than present
    /// with a zero, so the caller sums what it is given.
    /// </remarks>
    public sealed record VideoStreamStatusCountRow
    {
        /// <summary>Status the group belongs to.</summary>
        public required VideoStreamStatus Status { get; init; }

        /// <summary>How many streams carry that status.</summary>
        public required int Count { get; init; }
    }
}
