using Fluxy.Core.Vod.Enums;

namespace Fluxy.Core.Vod.Outcomes
{
    /// <summary>
    /// How one item inside a bulk operation ended.
    /// </summary>
    /// <remarks>
    /// <see cref="Action"/> is the very outcome the single-row service returned, so a bulk run
    /// and a single request spell the same fact the same way.
    /// </remarks>
    public sealed record BulkVodItemResult
    {
        /// <summary>The item this entry is about.</summary>
        public required Guid VodId { get; init; }

        /// <summary>How the single-row operation ended for this item.</summary>
        public required VodAction Action { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
