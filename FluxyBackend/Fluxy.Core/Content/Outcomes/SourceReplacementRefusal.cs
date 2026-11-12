using Fluxy.Core.Content.Enums;

namespace Fluxy.Core.Content.Outcomes
{
    /// <summary>
    /// One entity a source replacement declined to touch, and why.
    /// </summary>
    /// <remarks>
    /// A refusal is never a failure of the run: every other entity is still written and the
    /// request answers 200. It names the entity rather than the address so the operator can
    /// find the row, and the reason rather than a message so the transport and the screen own
    /// the wording.
    /// </remarks>
    public sealed record SourceReplacementRefusal
    {
        /// <summary>Which playback domain the refused entity belongs to.</summary>
        public required SourceDomain Domain { get; init; }

        /// <summary>Identifier of the entity - a stream, an item or an episode.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name of that entity, as the refusal line reads it.</summary>
        public required string Name { get; init; }

        /// <summary>Why the entity was left untouched.</summary>
        public required SourceRefusalReason Reason { get; init; }
    }
}
