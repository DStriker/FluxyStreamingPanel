using Fluxy.Core.Models.Authentication;
using Fluxy.Core.Models.Users;

namespace Fluxy.Core.Abstractions.Repositories
{
    /// <summary>
    /// Reads and writes the account rows and their groups.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The repository speaks models of this layer, never entities: a caller receives a
    /// <see cref="User"/> or an <see cref="AccountStanding"/> and never has to know that a row
    /// has a group navigation, a change tracker or a database representation at all.
    /// </para>
    /// <para>
    /// Reads that feed a decision return exactly the values the decision needs. The projection
    /// of <see cref="GetStandingAsync"/> is the clearest case: the authorization path must not
    /// fetch a password hash it will never compare, and the repository is the place that
    /// guarantees it does not.
    /// </para>
    /// </remarks>
    public interface IUserRepository
    {
        /// <summary>
        /// Finds the account with the given login name, with its group, or null when no row
        /// carries it.
        /// </summary>
        /// <param name="username">Login name, compared case sensitively.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<User?> FindByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds the account with the given identifier, with its group, or null when no row
        /// carries it.
        /// </summary>
        /// <param name="userId">Identifier of the account.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<User?> FindByIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the current state, role and permissions of an account in one query.
        /// </summary>
        /// <param name="userId">Identifier of the account.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The standing, with a null <see cref="AccountStanding.Status"/> when no row carries the
        /// identifier.
        /// </returns>
        Task<AccountStanding> GetStandingAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Whether the account's own row is <see cref="UserStatus.Registered"/>.
        /// </summary>
        /// <remarks>
        /// This is the gate the account-owned read endpoints apply before answering with
        /// anything about the account. It deliberately tests the row's own status, not the
        /// effective one: that is the question those endpoints have always asked, and changing
        /// it here would silently change who may read a session list.
        /// </remarks>
        /// <param name="userId">Identifier of the account.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> IsRegisteredAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Whether any account exists in a group at the administrator level.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> HasAdminAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the level an account holds through its group, or null when no row carries the
        /// identifier.
        /// </summary>
        /// <param name="userId">Identifier of the account.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<UserRole?> GetRoleAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a new account row for the next save.
        /// </summary>
        /// <param name="account">Values the row is written with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Identifier the new row carries.</returns>
        Task<Guid> AddAsync(
            NewAccount account,
            CancellationToken cancellationToken = default);
    }
}
