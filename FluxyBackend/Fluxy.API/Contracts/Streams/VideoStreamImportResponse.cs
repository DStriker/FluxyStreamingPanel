namespace Fluxy.API.Contracts.Streams
{
    /// <summary>
    /// The answer to a playlist import: how many entries the file held, how many landed, and
    /// what happened to the rest.
    /// </summary>
    /// <remarks>
    /// Always a <c>200</c> when the file was readable, even when nothing landed. A file whose
    /// every name is already taken is not a malformed request - the request was carried out and
    /// the answer is that there was nothing to add. A status can only describe the request as a
    /// whole, so folding a partial success into a <c>409</c> would make "four thousand imported"
    /// look like "nothing happened" and leave the client unable to redraw the list.
    /// </remarks>
    public sealed record VideoStreamImportResponse
    {
        /// <summary>Machine readable outcome of the import as a whole.</summary>
        public required string Code { get; init; }

        /// <summary>English fallback sentence naming what was done.</summary>
        public required string Message { get; init; }

        /// <summary>How many entries the file held, including the ones that could not be used.</summary>
        public required int Total { get; init; }

        /// <summary>How many streams were written.</summary>
        public required int Imported { get; init; }

        /// <summary>How many entries were left alone because their name was already taken.</summary>
        public required int Skipped { get; init; }

        /// <summary>How many existing streams were merged with import settings.</summary>
        public required int Updated { get; init; }

        /// <summary>How many entries could not become a stream.</summary>
        public required int Failed { get; init; }

        /// <summary>The entries that were skipped or refused, in the order of the file.</summary>
        public required IReadOnlyList<VideoStreamImportProblemResponse> Problems { get; init; }
    }
}
