using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Utils;

namespace Fluxy.Core.Streams.Commands
{
    /// <summary>
    /// One bulk operation over a set of audio streams: which operation, over which streams,
    /// and the single value the operation writes.
    /// </summary>
    /// <remarks>
    /// The identifiers travel explicitly, for the reason <c>BulkUserOperation</c> gives: the
    /// selection a table offers is the rows of the page the operator is looking at, so the
    /// request names them rather than describing them with a predicate the server would have
    /// to run before it could know what it was about to change.
    /// </remarks>
    public sealed record BulkAudioStreamOperation
    {
        /// <summary>What to do to every stream named in <see cref="StreamIds"/>.</summary>
        public required BulkAudioStreamAction Action { get; init; }

        /// <summary>
        /// The streams to act on, in the order the client listed them. At most
        /// <see cref="AudioStreamBulkLimits.MaxItems"/> of them; duplicates collapse in the
        /// transport rather than run an operation twice over the same row.
        /// </summary>
        public required IReadOnlyList<Guid> StreamIds { get; init; }

        /// <summary>Destination of a move, or null for the operations that have none.</summary>
        public Guid? CategoryId { get; init; }

        /// <summary>Destination of a type change, or null for the other operations.</summary>
        public AudioStreamType? Type { get; init; }

        /// <summary>User agent a restream change writes; null clears it.</summary>
        public string? RestreamUserAgent { get; init; }

        /// <summary>Proxy a restream change writes; null clears it.</summary>
        public string? RestreamProxy { get; init; }

        /// <summary>Cookies a restream change writes; null clears them.</summary>
        public string? RestreamCookies { get; init; }

        /// <summary>Extra headers a restream change writes; null clears them.</summary>
        public string? RestreamHeaders { get; init; }
    }
}
