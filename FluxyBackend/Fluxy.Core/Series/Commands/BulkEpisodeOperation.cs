using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Utils;

namespace Fluxy.Core.Series.Commands
{
    /// <summary>
    /// One bulk operation over a set of episodes of one series.
    /// </summary>
    public sealed record BulkEpisodeOperation
    {
        /// <summary>What to do to every episode named in <see cref="EpisodeIds"/>.</summary>
        public required BulkEpisodeAction Action { get; init; }

        /// <summary>
        /// The episodes to act on, in the order the client listed them. At most
        /// <see cref="EpisodeBulkLimits.MaxItems"/> of them; duplicates collapse in the transport.
        /// </summary>
        public required IReadOnlyList<Guid> EpisodeIds { get; init; }

        /// <summary>State a status change writes, or null for the other operations.</summary>
        public EpisodeStatus? Status { get; init; }

        /// <summary>Delivery kind a type change writes, or null for the other operations.</summary>
        public EpisodeType? Type { get; init; }
    }
}
