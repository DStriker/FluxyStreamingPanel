using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.ReadModels;

namespace Fluxy.Core.Streams.Interfaces
{
    /// <summary>
    /// Records the problems a stream producer reports, reads them a page at a time, and clears
    /// the journal on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="RecordAsync"/> is the one write path.</b> The producer that fails to play a
    /// channel asks this service to store what it said; no caller stages a row itself, which is
    /// what keeps the trimming, the blank-message refusal and the column cut in one place.
    /// </para>
    /// <para>
    /// <b>A failed record is not swallowed.</b> Unlike the user audit log, writing the row is the
    /// whole operation here, so an exception travels to the caller: the policy that decides
    /// whether to retry, skip or fail the playback belongs to the future producer, not to this
    /// service.
    /// </para>
    /// <para>
    /// The interface says nothing about HTTP, codes or cookies: which outcome becomes a 200 is the
    /// transport layer's business.
    /// </para>
    /// </remarks>
    public interface IStreamProblemService
    {
        /// <summary>Reads one filtered page of the journal, counted before paging.</summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many records one page holds.</param>
        /// <param name="search">Case-insensitive substring of the message, or null.</param>
        /// <param name="streamId">Stream to keep, or null for every stream.</param>
        /// <param name="dateFrom">Earliest <c>created_at</c> to keep, inclusive, or null.</param>
        /// <param name="dateTo">Latest <c>created_at</c> to keep, inclusive, or null.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<StreamProblemPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? streamId,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            StreamProblemSortField sortBy,
            StreamProblemSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes every row of the journal, whatever the reader had filtered the screen to.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>How many records were removed; zero when the table was already empty.</returns>
        Task<long> ClearAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages one problem row and commits it with its own save.
        /// </summary>
        /// <param name="problem">Which stream failed and what the producer reported.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// The message is trimmed and cut to <c>StreamProblemListLimits.ErrorMessageMaxLength</c>.
        /// A message that is blank once trimmed is refused rather than stored: an empty row is not
        /// evidence of anything, and a silent empty insert would hide the producer's bug instead of
        /// surfacing it.
        /// </remarks>
        /// <exception cref="ArgumentException">The message is blank.</exception>
        Task RecordAsync(NewStreamProblem problem, CancellationToken cancellationToken = default);
    }
}
