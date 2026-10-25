using Fluxy.Core.Series.Commands;
using Fluxy.Core.Series.Outcomes;

namespace Fluxy.Core.Series.Interfaces
{
    /// <summary>
    /// Applies one operation to a set of episodes of one series.
    /// </summary>
    public interface IEpisodeBulkService
    {
        /// <summary>Applies the operation and reports how each named episode ended.</summary>
        Task<EpisodeBulkOutcome> BulkAsync(
            Guid seriesId,
            BulkEpisodeOperation operation,
            CancellationToken cancellationToken = default);
    }
}
