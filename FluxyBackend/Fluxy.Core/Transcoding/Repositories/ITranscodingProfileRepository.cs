using Fluxy.Core.Transcoding.Commands;
using Fluxy.Core.Transcoding.Enums;
using Fluxy.Core.Transcoding.Models;
using Fluxy.Core.Transcoding.ReadModels;

namespace Fluxy.Core.Transcoding.Repositories
{
    /// <summary>
    /// Reads and writes the transcoding profile rows.
    /// </summary>
    /// <remarks>
    /// The repository owns the page query and the storage of a row. The service owns what a
    /// profile may contain, which refusal an outcome deserves, and whether a profile is still
    /// referenced.
    /// </remarks>
    public interface ITranscodingProfileRepository
    {
        /// <summary>Reads one filtered page, counted before paging.</summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many profiles one page holds.</param>
        /// <param name="search">Case-insensitive substring of the name, or null.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<TranscodingProfilePage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            TranscodingProfileSortField sortBy,
            TranscodingProfileSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>Reads one profile in full.</summary>
        /// <param name="id">Identifier of the profile.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<TranscodingProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Reads every profile as an option, ordered by name.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<IReadOnlyList<TranscodingProfileOption>> GetOptionsAsync(
            CancellationToken cancellationToken = default);

        /// <summary>Whether another profile already holds the name.</summary>
        /// <param name="name">Name to probe, compared exactly.</param>
        /// <param name="exceptProfile">Profile to compare against itself, or null for a create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> IsNameTakenAsync(
            string name,
            Guid? exceptProfile,
            CancellationToken cancellationToken = default);

        /// <summary>Whether a profile with this identifier exists.</summary>
        /// <param name="id">Identifier to probe.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// How many rows of the four referencing tables point at the profile: video streams,
        /// audio streams, on-demand videos and series.
        /// </summary>
        /// <param name="profileId">Identifier of the profile.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<int> CountReferencesAsync(
            Guid profileId,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a new profile.</summary>
        /// <param name="profile">Values the row is written with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Identifier the new row carries.</returns>
        Task<Guid> AddAsync(
            TranscodingProfileWrite profile,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a change to every option of a profile.</summary>
        /// <param name="id">Identifier of the profile.</param>
        /// <param name="profile">Values the row must carry after the change.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>False when no row carries the identifier.</returns>
        Task<bool> UpdateAsync(
            Guid id,
            TranscodingProfileWrite profile,
            CancellationToken cancellationToken = default);

        /// <summary>Stages the removal of a profile.</summary>
        /// <param name="id">Identifier of the profile.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>False when no row carries the identifier.</returns>
        Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
