using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Utils;

namespace Fluxy.Core.Streams.Commands
{
    /// <summary>
    /// One bulk operation over a set of streams: which operation, over which streams, and the
    /// single value the operation writes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The identifiers travel explicitly, for the reason <c>BulkUserOperation</c> gives: the
    /// selection a table offers is the rows of the page the operator is looking at, so the
    /// request names them rather than describing them with a predicate the server would have
    /// to run before it could know what it was about to change.
    /// </para>
    /// <para>
    /// Each payload property belongs to one operation and is ignored by the others. Absent
    /// where the one operation that needs it would silently change nothing is refused by the
    /// service rather than reinterpreted as "leave it alone".
    /// </para>
    /// </remarks>
    public sealed record BulkVideoStreamOperation
    {
        /// <summary>What to do to every stream named in <see cref="StreamIds"/>.</summary>
        public required BulkVideoStreamAction Action { get; init; }

        /// <summary>
        /// The streams to act on, in the order the client listed them. At most
        /// <see cref="VideoStreamBulkLimits.MaxItems"/> of them; duplicates collapse in the
        /// transport rather than run an operation twice over the same row.
        /// </summary>
        public required IReadOnlyList<Guid> StreamIds { get; init; }

        /// <summary>Destination of a move, or null for the operations that have none.</summary>
        public Guid? CategoryId { get; init; }

        /// <summary>
        /// Kind a type change writes. Only <see cref="VideoStreamType.Permanent"/>,
        /// <see cref="VideoStreamType.OnDemand"/> and <see cref="VideoStreamType.Redirect"/>
        /// are accepted, because a destination kind needs the payload this operation cannot
        /// invent: a series, an on-demand item or a file list.
        /// </summary>
        public VideoStreamType? Type { get; init; }

        /// <summary>Delay a delay change writes, in minutes.</summary>
        public int? DelayMinutes { get; init; }

        /// <summary>Archive length an archive change writes, in days.</summary>
        public int? ArchiveDays { get; init; }

        /// <summary>User agent a restream change writes, or null to clear it.</summary>
        public string? RestreamUserAgent { get; init; }

        /// <summary>Proxy a restream change writes, or null to clear it.</summary>
        public string? RestreamProxy { get; init; }

        /// <summary>Cookies a restream change writes, or null to clear them.</summary>
        public string? RestreamCookies { get; init; }

        /// <summary>Extra headers a restream change writes, or null to clear them.</summary>
        public string? RestreamHeaders { get; init; }
    }
}
