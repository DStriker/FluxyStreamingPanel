using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Outcomes
{
    /// <summary>
    /// One playlist line that did not become an audio stream.
    /// </summary>
    /// <remarks>
    /// The action tells a skipped entry from a failed one without making the client read the
    /// code: a skipped entry was a valid stream whose name another row already held, while a
    /// failed entry could not become a stream at all. Both carry the line so the operator can
    /// open the file at the right place.
    /// </remarks>
    public sealed record AudioStreamImportProblem
    {
        /// <summary>One-based line of the entry in the uploaded file.</summary>
        public required int LineNumber { get; init; }

        /// <summary>Name the entry would have carried, or null when the line named none.</summary>
        public string? Name { get; init; }

        /// <summary>What happened to the entry.</summary>
        public required AudioStreamAction Action { get; init; }

        /// <summary>English fallback sentence for the entry, when it has one.</summary>
        public string? Message { get; init; }

        /// <summary>Rejected fields, keyed by the camelCase name of the form field.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
