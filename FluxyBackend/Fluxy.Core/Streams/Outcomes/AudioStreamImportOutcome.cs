using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Outcomes
{
    /// <summary>
    /// The answer to a playlist import: how many entries the file held, how many landed, and
    /// what happened to the rest.
    /// </summary>
    /// <remarks>
    /// The counts are not decoration. <c>Total</c> is what the file held, <c>Imported</c> is
    /// what was written, <c>Skipped</c> is what was left alone because its name was taken,
    /// <c>Updated</c> is what was merged into an existing row, and <c>Failed</c> is what could
    /// not become a stream at all - so a client can say "four of five" without reading the
    /// problems list.
    /// </remarks>
    public sealed record AudioStreamImportOutcome
    {
        /// <summary>How the import as a whole ended.</summary>
        public required AudioStreamImportStatus Status { get; init; }

        /// <summary>Rejected fields of the request, when the batch itself was refused.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

        /// <summary>How many entries the file held, including the ones that could not be used.</summary>
        public int Total { get; init; }

        /// <summary>How many streams were written.</summary>
        public int Imported { get; init; }

        /// <summary>How many entries were left alone because their name was already taken.</summary>
        public int Skipped { get; init; }

        /// <summary>How many existing streams were merged with the import's settings.</summary>
        public int Updated { get; init; }

        /// <summary>How many entries could not become a stream.</summary>
        public int Failed { get; init; }

        /// <summary>One entry per line that did not land, in file order.</summary>
        public IReadOnlyList<AudioStreamImportProblem> Problems { get; init; } = [];
    }
}
