using Fluxy.Core.Series.Commands;
using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Models;
using Fluxy.Core.Series.ReadModels;

namespace Fluxy.Core.Series.Repositories
{
    /// <summary>
    /// Reads and writes the series rows and the metadata stored on them.
    /// </summary>
    /// <remarks>
    /// The repository owns the page query, the category-scoped name probe and the storage of
    /// the two arrays. The service owns what a series may contain: which values are usable, how
    /// a patch resolves "keep" against "clear", and which refusal an outcome deserves.
    /// </remarks>
    public interface ISeriesRepository
    {
        /// <summary>Reads one filtered page of the table, counted before paging.</summary>
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

        /// <summary>
        /// Finds one series for a change, with the row left tracked. There are no child lists
        /// to load: the cast and the genres are columns on the row.
        /// </summary>
        Task<SeriesItem?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Whether the identifier names an existing category of series.</summary>
        Task<bool> CategoryIsUsableAsync(Guid categoryId, CancellationToken cancellationToken = default);

        /// <summary>Whether another series of the same category already holds the name.</summary>
        Task<bool> IsNameTakenAsync(
            string name,
            Guid categoryId,
            Guid? exceptSeries,
            CancellationToken cancellationToken = default);

        /// <summary>Whether a series with the identifier exists.</summary>
        Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Stages a new series in its category.</summary>
        Task<Guid> AddAsync(SeriesWrite series, CancellationToken cancellationToken = default);

        /// <summary>Stages a change of the series' fields.</summary>
        Task<bool> UpdateAsync(
            Guid id,
            SeriesWrite series,
            CancellationToken cancellationToken = default);

        /// <summary>Stages the removal of a series and, by cascade, its episodes.</summary>
        Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
