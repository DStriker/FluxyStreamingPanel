namespace Fluxy.API.Contracts.Vod
{
    /// <summary>
    /// One playlist line that did not become a video.
    /// </summary>
    /// <remarks>
    /// <see cref="Skipped"/> tells the two answers apart without making the client read the
    /// code: a skipped entry was a valid video whose name another row already held, while a
    /// failed entry could not become a video at all. Both carry the line so the operator can
    /// open the file at the right place.
    /// </remarks>
    public sealed record VodImportProblemResponse
    {
        /// <summary>One-based line of the entry in the uploaded file.</summary>
        public required int Line { get; init; }

        /// <summary>Name the entry would have carried, or null when the line named none.</summary>
        public string? Name { get; init; }

        /// <summary>Whether the entry was left alone rather than refused.</summary>
        public required bool Skipped { get; init; }

        /// <summary>Machine readable name of what happened.</summary>
        public required string Code { get; init; }

        /// <summary>English fallback sentence for the entry.</summary>
        public required string Message { get; init; }

        /// <summary>Rejected fields, keyed by the camelCase name of the form field.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
