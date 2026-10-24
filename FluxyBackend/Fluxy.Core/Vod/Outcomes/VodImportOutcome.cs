using Fluxy.Core.Vod.Enums;

namespace Fluxy.Core.Vod.Outcomes
{
    /// <summary>
    /// The whole of a playlist import: how it ended, what it counted, and what it could not use.
    /// </summary>
    /// <remarks>
    /// The counts add up to <see cref="Total"/> and are stated separately because "already had
    /// that name" (skipped) and "the line is not usable" (failed) are different things an
    /// operator may react to differently.
    /// </remarks>
    public sealed record VodImportOutcome
    {
        /// <summary>How the import ended as a whole.</summary>
        public required VodImportStatus Status { get; init; }

        /// <summary>Rejected settings, when the request itself was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

        /// <summary>How many entries the file held, including the ones that could not be used.</summary>
        public int Total { get; init; }

        /// <summary>How many items were written.</summary>
        public int Imported { get; init; }

        /// <summary>How many entries were left alone because their name was already taken.</summary>
        public int Skipped { get; init; }

        /// <summary>How many entries could not become an item.</summary>
        public int Failed { get; init; }

        /// <summary>The entries that were skipped or refused, in playlist order.</summary>
        public IReadOnlyList<VodImportProblem> Problems { get; init; } = [];
    }
}
