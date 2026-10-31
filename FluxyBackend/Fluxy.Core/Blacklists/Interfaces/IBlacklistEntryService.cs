using Fluxy.Core.Blacklists.Commands;
using Fluxy.Core.Blacklists.Enums;
using Fluxy.Core.Blacklists.Outcomes;
using Fluxy.Core.Blacklists.ReadModels;

namespace Fluxy.Core.Blacklists.Interfaces
{
    /// <summary>
    /// Reads one blacklist and changes its entries on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// The enforcement that will refuse traffic and increment the counter is not part of this
    /// interface: it is a later pass and it reads the same table directly. What is here is the
    /// operator's side of the domain - the three lists, their entries and the rules an entry
    /// must satisfy.
    /// </remarks>
    public interface IBlacklistEntryService
    {
        /// <summary>Reads one filtered page of one list, counted before paging.</summary>
        /// <param name="type">Which of the three lists to read.</param>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many entries one page holds.</param>
        /// <param name="search">Case-insensitive substring of the value or the note, or null.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<BlacklistEntryPage> GetPageAsync(
            BlacklistType type,
            int page,
            int pageSize,
            string? search,
            BlacklistSortField sortBy,
            BlacklistSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>Reads one entry as the API answers with it.</summary>
        /// <param name="id">Identifier of the entry.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The entry, or null when there is no such one.</returns>
        Task<BlacklistEntryView?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Creates an entry in one list.</summary>
        /// <param name="input">The list, the value and the note.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, carrying the identifier once the row exists.</returns>
        Task<BlacklistEntryOutcome> CreateAsync(
            NewBlacklistEntry input,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Changes the value and the note of an entry. The list, the counter and the added date
        /// are left exactly as they are.
        /// </summary>
        /// <param name="id">Identifier of the entry to change.</param>
        /// <param name="patch">What to change about it.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<BlacklistEntryOutcome> UpdateAsync(
            Guid id,
            BlacklistEntryPatch patch,
            CancellationToken cancellationToken = default);

        /// <summary>Deletes an entry.</summary>
        /// <param name="id">Identifier of the entry to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<BlacklistEntryOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes every entry of one list at once.
        /// </summary>
        /// <remarks>
        /// The list type is the whole of what the command names, and nothing narrows it: an
        /// applied search is a property of the view the operator is looking at, while "clear
        /// the list" is a statement about the list itself. A clear narrowed by a filter would
        /// remove some rows and leave a screen that looks empty with rows still behind it.
        /// </remarks>
        /// <param name="type">Which of the three lists to empty.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<BlacklistEntryOutcome> ClearAsync(
            BlacklistType type,
            CancellationToken cancellationToken = default);
    }
}
