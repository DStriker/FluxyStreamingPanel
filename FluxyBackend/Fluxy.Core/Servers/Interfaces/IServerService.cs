using Fluxy.Core.Servers.Commands;
using Fluxy.Core.Servers.Enums;
using Fluxy.Core.Servers.Outcomes;
using Fluxy.Core.Servers.ReadModels;

namespace Fluxy.Core.Servers.Interfaces
{
    /// <summary>
    /// Reads the servers and changes them on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// The main server is a row this interface can read and write like any other, and the one
    /// thing it cannot do is remove it: <see cref="DeleteAsync"/> answers
    /// <see cref="ServerAction.ImmutableMain"/> for it. Measuring a server - the process that
    /// would write its latency - is not part of this interface; the row stores the value and an
    /// operator may clear it.
    /// </remarks>
    public interface IServerService
    {
        /// <summary>Reads one filtered page of the table, counted before paging.</summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many servers one page holds.</param>
        /// <param name="search">
        /// Case-insensitive substring of the name, the host name or the address, or null.
        /// </param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<ServerPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            ServerSortField sortBy,
            ServerSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>Reads one server as the API answers with it.</summary>
        /// <param name="id">Identifier of the server.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The server, or null when there is no such one.</returns>
        Task<ServerView?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads every server as a picker entry, ordered by name - what the subscription and
        /// stream forms draw.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<IReadOnlyList<ServerOption>> GetOptionsAsync(
            CancellationToken cancellationToken = default);

        /// <summary>Creates a server.</summary>
        /// <param name="input">What the operator supplied.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, carrying the identifier once the row exists.</returns>
        Task<ServerOutcome> CreateAsync(
            NewServer input,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes every editable field of a server. The patch is the whole row, not a set of
        /// changes - see <see cref="ServerPatch"/>.
        /// </summary>
        /// <param name="id">Identifier of the server to write.</param>
        /// <param name="patch">Values the row must carry afterwards.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<ServerOutcome> UpdateAsync(
            Guid id,
            ServerPatch patch,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a server. The main server is refused with
        /// <see cref="ServerAction.ImmutableMain"/> and a referenced row with
        /// <see cref="ServerAction.InUse"/>; either way the row survives the refusal.
        /// </summary>
        /// <param name="id">Identifier of the server to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<ServerOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
