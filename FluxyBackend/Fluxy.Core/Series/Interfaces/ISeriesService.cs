using Fluxy.Core.Series.Commands;
using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Outcomes;
using Fluxy.Core.Series.ReadModels;

namespace Fluxy.Core.Series.Interfaces
{
    /// <summary>
    /// Reads the series catalogue and changes it on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// The interface says nothing about HTTP, codes or cookies. It reports
    /// <see cref="SeriesAction"/>, and which of those becomes a 201 and which a 409 is the
    /// transport layer's business.
    /// </remarks>
    public interface ISeriesService
    {
        /// <summary>Reads one page of the series table, filtered and ordered the way the caller asked.</summary>
        Task<SeriesPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? categoryId,
            SeriesStatus? status,
            SeriesSortField sortBy,
            SeriesSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>Reads one series in full, with the two counts over its episodes.</summary>
        Task<SeriesDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Whether a series with the identifier exists.</summary>
        Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Creates a series in its category.</summary>
        Task<SeriesOutcome> CreateAsync(
            NewSeries input,
            CancellationToken cancellationToken = default);

        /// <summary>Changes a series. Every field a body can name is optional.</summary>
        Task<SeriesOutcome> UpdateAsync(
            Guid id,
            SeriesPatch patch,
            CancellationToken cancellationToken = default);

        /// <summary>Deletes a series together with its episodes.</summary>
        Task<SeriesOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
