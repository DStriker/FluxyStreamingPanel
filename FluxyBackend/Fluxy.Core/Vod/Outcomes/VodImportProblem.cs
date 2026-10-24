using Fluxy.Core.Vod.Enums;

namespace Fluxy.Core.Vod.Outcomes
{
    /// <summary>
    /// One playlist entry that was skipped or refused while the rest of the file was imported.
    /// </summary>
    /// <remarks>
    /// <see cref="Action"/> is the same word the single-row service uses:
    /// <see cref="VodAction.AlreadyExists"/> is a skipped row and <see cref="VodAction.Invalid"/>
    /// is a failed one. The transport decides which code each deserves.
    /// </remarks>
    public sealed record VodImportProblem
    {
        /// <summary>One-based line of the entry in the uploaded file.</summary>
        public required int LineNumber { get; init; }

        /// <summary>Name the entry would have carried, or null when the line named none.</summary>
        public string? Name { get; init; }

        /// <summary>Whether the entry was skipped or failed, using the single-row vocabulary.</summary>
        public required VodAction Action { get; init; }

        /// <summary>A sentence of the file itself, when the parser produced one.</summary>
        public string? Message { get; init; }

        /// <summary>Rejected fields, when the entry itself was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
