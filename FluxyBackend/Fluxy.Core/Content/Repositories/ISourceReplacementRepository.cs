using Fluxy.Core.Content.Rows;

namespace Fluxy.Core.Content.Repositories
{
    /// <summary>
    /// The data access contract of a mass source replacement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Reads are candidate reads, not the whole table.</b> Each one narrows its table with
    /// the search text so the run touches only rows that can change; the service re-checks
    /// every candidate ordinally, because the SQL comparison is a filter rather than the rule.
    /// </para>
    /// <para>
    /// <b>Writes mutate existing child rows in place and never save.</b> A set intent changes
    /// only the address column of the row it names - the identifier, the position and the audit
    /// columns are left alone - and the caller commits everything once, after every rule has
    /// passed.
    /// </para>
    /// </remarks>
    public interface ISourceReplacementRepository
    {
        /// <summary>Reads the video stream sources whose address can contain the search text.</summary>
        /// <param name="find">Text the stored address must contain to be a candidate.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<IReadOnlyList<StoredStreamSourceRow>> GetStreamSourceCandidatesAsync(
            string find,
            CancellationToken cancellationToken = default);

        /// <summary>Reads the audio stream sources whose address can contain the search text.</summary>
        /// <param name="find">Text the stored address must contain to be a candidate.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<IReadOnlyList<StoredAudioSourceRow>> GetAudioSourceCandidatesAsync(
            string find,
            CancellationToken cancellationToken = default);

        /// <summary>Reads the on-demand items whose source can contain the search text.</summary>
        /// <param name="find">Text the stored source must contain to be a candidate.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<IReadOnlyList<StoredVodSourceRow>> GetVodSourceCandidatesAsync(
            string find,
            CancellationToken cancellationToken = default);

        /// <summary>Reads the series episodes whose source can contain the search text.</summary>
        /// <param name="find">Text the stored source must contain to be a candidate.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<IReadOnlyList<StoredEpisodeSourceRow>> GetEpisodeSourceCandidatesAsync(
            string find,
            CancellationToken cancellationToken = default);

        /// <summary>Stages the new addresses of one video stream's failover chain.</summary>
        /// <param name="streamId">Stream whose source rows are changed.</param>
        /// <param name="urlsByPosition">New address per position; every other position is left alone.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetStreamSourcesAsync(
            Guid streamId,
            IReadOnlyDictionary<int, string> urlsByPosition,
            CancellationToken cancellationToken = default);

        /// <summary>Stages the new addresses of one audio stream's failover chain.</summary>
        /// <param name="streamId">Stream whose source rows are changed.</param>
        /// <param name="urlsByPosition">New address per position; every other position is left alone.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetAudioSourcesAsync(
            Guid streamId,
            IReadOnlyDictionary<int, string> urlsByPosition,
            CancellationToken cancellationToken = default);

        /// <summary>Stages the new source of one on-demand item.</summary>
        /// <param name="id">Identifier of the item.</param>
        /// <param name="source">Address the row must carry from now on.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetVodSourceAsync(
            Guid id,
            string source,
            CancellationToken cancellationToken = default);

        /// <summary>Stages the new source of one series episode.</summary>
        /// <param name="id">Identifier of the episode.</param>
        /// <param name="source">Address the row must carry from now on.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetEpisodeSourceAsync(
            Guid id,
            string source,
            CancellationToken cancellationToken = default);
    }
}
