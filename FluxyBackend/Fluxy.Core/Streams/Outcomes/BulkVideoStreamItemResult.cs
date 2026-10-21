using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Outcomes
{
    /// <summary>
    /// How one stream inside a bulk operation ended.
    /// </summary>
    /// <remarks>
    /// <see cref="Action"/> is the very outcome the single-row service returned, so a bulk run
    /// and a single request spell the same fact the same way. <see cref="Warnings"/> is empty
    /// for every operation except a type change that had to clear a setting the destination
    /// kind cannot carry - the change itself landed.
    /// </remarks>
    public sealed record BulkVideoStreamItemResult
    {
        /// <summary>The stream this entry is about.</summary>
        public required Guid StreamId { get; init; }

        /// <summary>How the single-row operation ended for this stream.</summary>
        public required VideoStreamAction Action { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

        /// <summary>
        /// Settings the operation had to clear, or null when nothing was cleared. A warning
        /// never accompanies a refused row: the row was not written, so nothing was cleared.
        /// </summary>
        public IReadOnlyList<VideoStreamBulkWarning>? Warnings { get; init; }
    }
}
