using Fluxy.Core.Series.Commands;
using Fluxy.Core.Series.Outcomes;

namespace Fluxy.Core.Series.Interfaces
{
    /// <summary>
    /// Applies one operation to a set of series, one series at a time.
    /// </summary>
    public interface ISeriesBulkService
    {
        /// <summary>Applies the operation and reports how each named series ended.</summary>
        Task<SeriesBulkOutcome> BulkAsync(
            BulkSeriesOperation operation,
            CancellationToken cancellationToken = default);
    }
}
