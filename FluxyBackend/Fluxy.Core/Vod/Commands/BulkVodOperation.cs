using Fluxy.Core.Vod.Enums;
using Fluxy.Core.Vod.Utils;

namespace Fluxy.Core.Vod.Commands
{
    /// <summary>
    /// One bulk operation over a set of on-demand items: which operation, over which items,
    /// and the single value the operation writes.
    /// </summary>
    /// <remarks>
    /// Each payload property belongs to one operation and is ignored by the others. Absent
    /// where the one operation that needs it would silently change nothing is refused by the
    /// service rather than reinterpreted as "leave it alone".
    /// </remarks>
    public sealed record BulkVodOperation
    {
        /// <summary>What to do to every item named in <see cref="VodIds"/>.</summary>
        public required BulkVodAction Action { get; init; }

        /// <summary>
        /// The items to act on, in the order the client listed them. At most
        /// <see cref="VodBulkLimits.MaxItems"/> of them; duplicates collapse in the transport.
        /// </summary>
        public required IReadOnlyList<Guid> VodIds { get; init; }

        /// <summary>Destination of a move, or null for the operations that have none.</summary>
        public Guid? CategoryId { get; init; }

        /// <summary>State a status change writes, or null for the other operations.</summary>
        public VodStatus? Status { get; init; }

        /// <summary>Delivery kind a type change writes, or null for the other operations.</summary>
        public VodType? Type { get; init; }
    }
}
