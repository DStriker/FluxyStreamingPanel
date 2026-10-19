using Fluxy.Core.Models.Users;
using Fluxy.Core.Persistence.Interfaces;

namespace Fluxy.Core.Abstractions.Repositories
{
    /// <summary>
    /// Reads the login guard of an account: its switch and its allow lists.
    /// </summary>
    /// <remarks>
    /// The switch is a column of the account row while the lists are rows of their own, which is
    /// why reading the guard is two reads rather than one. A repository method that joined them
    /// would either return the same account row several times or hide which of the two questions
    /// it really answered.
    /// </remarks>
    public interface ILoginGuardRepository
    {
        /// <summary>
        /// Reads whether sign-ins from outside the allow lists are refused for this account, or
        /// null when no account carries the identifier.
        /// </summary>
        /// <param name="userId">Identifier of the account.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool?> GetGeoProtectionEnabledAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads every allow list entry of the account, in no particular order.
        /// </summary>
        /// <param name="userId">Identifier of the account.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<IReadOnlyList<LoginGuardRule>> GetRulesAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages the two switches and replaces the account's allow lists.
        /// </summary>
        /// <remarks>
        /// The old rows are removed through the change tracker, because the table has no
        /// identity a caller reads and replacing the lists is the only write it supports. The
        /// deletion, the new rows and the account's switches are then committed by one
        /// <see cref="IUnitOfWork.SaveChangesAsync"/> call: together they are one transaction,
        /// so a guard is never half applied and a failed save leaves the previous guard intact.
        /// </remarks>
        /// <param name="userId">Account whose guard is written.</param>
        /// <param name="guard">Canonical switches and lists to store.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task ApplyAsync(
            Guid userId,
            NormalizedLoginGuard guard,
            CancellationToken cancellationToken = default);
    }
}
