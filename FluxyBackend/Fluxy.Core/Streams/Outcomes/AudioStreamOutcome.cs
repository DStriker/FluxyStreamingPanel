using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Outcomes
{
    /// <summary>
    /// How one write to an audio stream ended, and what the caller needs to know about it.
    /// </summary>
    /// <remarks>
    /// The service answers with this rather than an HTTP status, because the rules that decide
    /// an outcome and the mapping from an outcome to a status belong to different layers. A
    /// refusal carries the field errors the form draws beside the inputs; a success carries the
    /// identifier of the row it touched, when there is one.
    /// </remarks>
    public sealed record AudioStreamOutcome
    {
        /// <summary>What happened to the stream.</summary>
        public required AudioStreamAction Action { get; init; }

        /// <summary>Identifier of the stream, when the operation addressed an existing row.</summary>
        public Guid StreamId { get; init; }

        /// <summary>Rejected fields, keyed by the camelCase name the client sent.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
