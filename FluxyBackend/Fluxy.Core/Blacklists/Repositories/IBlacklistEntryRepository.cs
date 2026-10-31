using Fluxy.Core.Blacklists.Commands;
using Fluxy.Core.Blacklists.Enums;
using Fluxy.Core.Blacklists.Models;
using Fluxy.Core.Blacklists.ReadModels;

namespace Fluxy.Core.Blacklists.Repositories
{
    /// <summary>
    /// Reads and writes the entries of the three blacklists.
    /// </summary>
    /// <remarks>
    /// The repository owns the page queries and the storage of a row. The service owns what an
    /// entry may contain and which refusal an outcome deserves. A page is always scoped to one
    /// list, because the three screens are three lists over one table and a query that could
    /// cross them would be one no screen asks for.
    /// </remarks>
    public interface IBlacklistEntryRepository
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

        /// <summary>Reads one entry in full.</summary>
        /// <param name="id">Identifier of the entry.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<BlacklistEntry?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Whether the list already holds the value.</summary>
        /// <param name="type">The list to probe.</param>
        /// <param name="value">Value to probe, compared exactly.</param>
        /// <param name="exceptEntry">Entry to compare against itself, or null for a create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> IsValueTakenAsync(
            BlacklistType type,
            string value,
            Guid? exceptEntry,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a new entry. The counter starts at zero.</summary>
        /// <param name="type">Which of the three lists the entry belongs to.</param>
        /// <param name="entry">Values the row is written with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Identifier the new row carries.</returns>
        Task<Guid> AddAsync(
            BlacklistType type,
            BlacklistEntryWrite entry,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a change to the value and the note. The counter, the type and the added date
        /// are left exactly as they are.
        /// </summary>
        /// <param name="id">Identifier of the entry.</param>
        /// <param name="entry">Values the row must carry after the change.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>False when no row carries the identifier.</returns>
        Task<bool> UpdateAsync(
            Guid id,
            BlacklistEntryWrite entry,
            CancellationToken cancellationToken = default);

        /// <summary>Stages the removal of an entry.</summary>
        /// <param name="id">Identifier of the entry.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>False when no row carries the identifier.</returns>
        Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes every row of one list.
        /// </summary>
        /// <remarks>
        /// Delete-by-set rather than "load them and remove them one by one": a list an operator
        /// clears may hold thousands of entries, and the command's meaning is the set, not the
        /// rows. It is immediate, like every <c>ExecuteDelete</c> - there is nothing to stage
        /// and nothing around it in the same transaction that could fail afterwards.
        /// </remarks>
        /// <param name="type">Which of the three lists to empty.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>How many rows were removed.</returns>
        Task<int> RemoveAllAsync(BlacklistType type, CancellationToken cancellationToken = default);
    }
}
