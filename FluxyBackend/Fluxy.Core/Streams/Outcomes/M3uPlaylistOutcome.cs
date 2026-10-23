using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Outcomes
{
    /// <summary>
    /// The result of reading one playlist: its status, the entries that can be imported, and
    /// the lines that cannot.
    /// </summary>
    /// <remarks>
    /// A parsed playlist may carry both entries and problems. The parser reports an unusable
    /// line and keeps reading exactly so one bad line does not discard a file of good ones.
    /// </remarks>
    public sealed record M3uPlaylistOutcome
    {
        /// <summary>How reading ended.</summary>
        public required M3uPlaylistStatus Status { get; init; }

        /// <summary>Entries that can be turned into streams, in playlist order.</summary>
        public IReadOnlyList<M3uPlaylistEntry> Entries { get; init; } = [];

        /// <summary>Lines that could not become entries, in playlist order.</summary>
        public IReadOnlyList<M3uPlaylistProblem> Problems { get; init; } = [];
    }
}
