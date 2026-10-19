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
        /// Reads the profile page's fields and the permissions the account's group grants.
        /// </summary>
        /// <param name="userId">Identifier of the account.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<StoredProfile?> GetProfileAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds an account for a change, with its group and both halves of its status.
        /// </summary>
        /// <remarks>
        /// The row is read tracked so the intent methods that follow can stage their writes
        /// without reading it a second time. The model is still immutable: tracking is an
        /// implementation detail the caller cannot observe.
        /// </remarks>
        /// <param name="userId">Identifier of the account.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<EditableAccount?> FindEditableByIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds an account by its email address for a confirmation, with its group.
        /// </summary>
        /// <param name="email">Email address, compared case sensitively.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<EditableAccount?> FindEditableByEmailAsync(
            string email,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds the account with the given login name for a registration request.
        /// </summary>
        /// <param name="username">Login name, compared case sensitively.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<StoredAccount?> FindForRegistrationByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds the account with the given email address for a registration request.
        /// </summary>
        /// <param name="email">Email address, compared case sensitively.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<StoredAccount?> FindForRegistrationByEmailAsync(
            string email,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds the account with the given login name for the public password reset flow.
        /// </summary>
        /// <param name="username">Login name, compared case sensitively.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<StoredAccount?> FindForPasswordResetAsync(
            string username,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Whether an account other than <paramref name="exceptUserId"/> already holds the name.
        /// </summary>
        /// <param name="username">Login name, compared case sensitively.</param>
        /// <param name="exceptUserId">Account to compare against itself.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> IsUsernameTakenAsync(
            string username,
            Guid exceptUserId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Whether an account other than <paramref name="exceptUserId"/> already holds the
        /// address.
        /// </summary>
        /// <param name="email">Email address, compared case sensitively.</param>
        /// <param name="exceptUserId">Account to compare against itself.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> IsEmailTakenAsync(
            string email,
            Guid exceptUserId,
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

        /// <summary>
        /// Stages the password and the fresh code of an unfinished registration.
        /// </summary>
        /// <remarks>
        /// The row is expected to have been read by <see cref="FindForRegistrationByUsernameAsync"/>
        /// or <see cref="FindForRegistrationByEmailAsync"/> in the same request. When it is no
        /// longer tracked, the repository reads it again rather than guessing.
        /// </remarks>
        /// <param name="userId">Account whose registration is being replaced.</param>
        /// <param name="passwordHash">BCrypt hash of the new password.</param>
        /// <param name="codeHash">BCrypt hash of the new confirmation code.</param>
        /// <param name="expiresAt">Moment the new code stops being accepted.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task UpdatePendingRegistrationAsync(
            Guid userId,
            string passwordHash,
            string codeHash,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages the transition of an account from <see cref="UserStatus.Unregistered"/> to
        /// <see cref="UserStatus.Registered"/> and spends its registration code.
        /// </summary>
        /// <param name="userId">Account being confirmed.</param>
        /// <param name="registeredAt">Moment the account left Unregistered.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task ConfirmRegistrationAsync(
            Guid userId,
            DateTimeOffset registeredAt,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a new login name on the account's row.
        /// </summary>
        /// <param name="userId">Account being changed.</param>
        /// <param name="username">Name the row must carry.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetUsernameAsync(
            Guid userId,
            string username,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a new email address on the account's row.
        /// </summary>
        /// <param name="userId">Account being changed.</param>
        /// <param name="email">Address the row must carry.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetEmailAsync(
            Guid userId,
            string email,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a new password hash on the account's row.
        /// </summary>
        /// <param name="userId">Account being changed.</param>
        /// <param name="passwordHash">BCrypt hash the row must carry.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetPasswordHashAsync(
            Guid userId,
            string passwordHash,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a display time zone on the account's row; null clears the preference.
        /// </summary>
        /// <param name="userId">Account being changed.</param>
        /// <param name="timeZone">Canonical IANA identifier, or null for the browser default.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetTimeZoneAsync(
            Guid userId,
            string? timeZone,
            CancellationToken cancellationToken = default);
    }
}
