using Fluxy.Core.Transcoding.Commands;
using Fluxy.Core.Transcoding.Enums;
using Fluxy.Core.Transcoding.Outcomes;
using Fluxy.Core.Transcoding.ReadModels;

namespace Fluxy.Core.Transcoding.Interfaces
{
    /// <summary>
    /// Reads the transcoding profiles and changes them on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// The encoder that will spend these options is not part of this interface: it is a later
    /// pass and it reads the same table directly. What is here is the operator's side of the
    /// domain - the list, one profile, the picker the content forms draw, and the rules a
    /// profile must satisfy.
    /// </remarks>
    public interface ITranscodingProfileService
    {
        /// <summary>Reads one filtered page of the profile table, counted before paging.</summary>
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

        /// <summary>Reads one profile as the API answers with it.</summary>
        /// <param name="id">Identifier of the profile.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The profile, or null when there is no such one.</returns>
        Task<TranscodingProfileView?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads every profile as an option, ordered by name - what the four content forms draw.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<IReadOnlyList<TranscodingProfileOption>> GetOptionsAsync(
            CancellationToken cancellationToken = default);

        /// <summary>Creates a profile.</summary>
        /// <param name="input">The options the profile carries.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, carrying the identifier once the row exists.</returns>
        Task<TranscodingProfileOutcome> CreateAsync(
            NewTranscodingProfile input,
            CancellationToken cancellationToken = default);

        /// <summary>Changes the options of a profile.</summary>
        /// <param name="id">Identifier of the profile to change.</param>
        /// <param name="patch">What to change about it.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<TranscodingProfileOutcome> UpdateAsync(
            Guid id,
            TranscodingProfilePatch patch,
            CancellationToken cancellationToken = default);

        /// <summary>Deletes a profile nothing references.</summary>
        /// <param name="id">Identifier of the profile to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<TranscodingProfileOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
