using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Outcomes
{
    /// <summary>
    /// The whole of a playlist import: how it ended, what it counted, and what it could not use.
    /// </summary>
    /// <remarks>
    /// The counts add up to <see cref="Total"/> and are stated separately rather than derived by
    /// a client, because "already had that name" (skipped) and "the line is not usable" (failed)
    /// are different things an operator may react to differently. <see cref="Problems"/> holds
    /// only the entries that did not land; a successful entry has nothing to report.
    /// </remarks>
    public sealed record VideoStreamImportOutcome
    {
        /// <summary>How the import ended as a whole.</summary>
        public required VideoStreamImportStatus Status { get; init; }

        /// <summary>Rejected settings, when the request itself was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

        /// <summary>How many entries the file held, including the ones that could not be used.</summary>
        public int Total { get; init; }

        /// <summary>How many streams were written.</summary>
        public int Imported { get; init; }

        /// <summary>How many entries were left alone because their name was already taken.</summary>
        public int Skipped { get; init; }

        /// <summary>How many existing streams were merged with import settings.</summary>
        public int Updated { get; init; }

        /// <summary>How many entries could not become a stream.</summary>
        public int Failed { get; init; }

        /// <summary>The entries that were skipped or refused, in playlist order.</summary>
        public IReadOnlyList<VideoStreamImportProblem> Problems { get; init; } = [];
    }
}
