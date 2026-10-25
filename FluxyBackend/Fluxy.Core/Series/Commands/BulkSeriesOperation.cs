using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Utils;

namespace Fluxy.Core.Series.Commands
{
    /// <summary>
    /// One bulk operation over a set of series: which operation, over which series, and the
    /// single value the operation writes.
    /// </summary>
    public sealed record BulkSeriesOperation
    {
        /// <summary>What to do to every series named in <see cref="SeriesIds"/>.</summary>
        public required BulkSeriesAction Action { get; init; }

        /// <summary>
        /// The series to act on, in the order the client listed them. At most
        /// <see cref="SeriesBulkLimits.MaxItems"/> of them; duplicates collapse in the transport.
        /// </summary>
        public required IReadOnlyList<Guid> SeriesIds { get; init; }

        /// <summary>Destination of a move, or null for the operations that have none.</summary>
        public Guid? CategoryId { get; init; }

        /// <summary>State a status change writes, or null for the other operations.</summary>
        public SeriesStatus? Status { get; init; }
    }
}
