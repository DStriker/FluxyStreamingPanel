using Fluxy.Core.Models.Users;

namespace Fluxy.Core.Abstractions.Repositories
{
    /// <summary>
    /// Reads and writes the staged changes that wait for a one-time code.
    /// </summary>
    /// <remarks>
    /// One row per account, enforced by a unique index. <see cref="StageAsync"/> therefore
    /// updates the row the account already has or inserts one, and the caller keeps the
    /// existing catch for a lost race: a second request that inserted first is refused by the
    /// index, not by a read that happened a moment earlier.
    /// </remarks>
    public interface IPendingChangeRepository
    {
        /// <summary>
        /// Finds the staged change of one account, or null when it is waiting for nothing.
        /// </summary>
        /// <param name="userId">Account whose row is wanted.</param>
        /// <param name="kind">Kind the row must have, or null for whatever it has.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<StoredPendingChange?> FindByUserIdAsync(
            Guid userId,
            PendingChangeKind? kind = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a change for the next save: replaces the account's row when it has one,
        /// inserts it otherwise.
        /// </summary>
        /// <param name="change">Values the row is written with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task StageAsync(
            StoredPendingChange change,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages the removal of one staged change.
        /// </summary>
        /// <param name="id">Identifier of the row to remove.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task RemoveAsync(
            Guid id,
            CancellationToken cancellationToken = default);
    }
}
