using Fluxy.Core.Series.Enums;

namespace Fluxy.Core.Series.Outcomes
{
    /// <summary>
    /// How one episode inside a bulk operation ended.
    /// </summary>
    public sealed record EpisodeBulkItemResult
    {
        /// <summary>The episode this entry is about.</summary>
        public required Guid EpisodeId { get; init; }

        /// <summary>How the single-row operation ended for this episode.</summary>
        public required EpisodeAction Action { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
