using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Outcomes
{
    /// <summary>
    /// What happened to one stream of a bulk operation.
    /// </summary>
    /// <remarks>
    /// One entry per identifier the request named, in the order it named them. The action is
    /// the same vocabulary a single-row write answers with, so a client that already draws one
    /// stream's outcome can draw a batch without a second table of cases.
    /// </remarks>
    public sealed record BulkAudioStreamItemResult
    {
        /// <summary>Identifier of the stream this entry is about.</summary>
        public required Guid StreamId { get; init; }

        /// <summary>What happened to that stream.</summary>
        public required AudioStreamAction Action { get; init; }

        /// <summary>Rejected fields, keyed by the camelCase name the client sent.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
