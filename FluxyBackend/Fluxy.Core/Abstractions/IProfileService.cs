using Fluxy.Core.Models.Users;

namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Reads and changes the profile of an account that is already signed in.
    /// </summary>
    /// <remarks>
    /// Every change here is confirmed by a code mailed to the account, except on an
    /// installation with no mail server, where there is nothing to confirm with and the change
    /// is applied at once - the service decides that, the caller does not ask. The current
    /// password is required for all three operations regardless: a session that is left open on
    /// a shared machine is not proof of anything, and the password is.
    ///
    /// The interface says nothing about HTTP, codes or cookies. It reports
    /// <see cref="ProfileChangeStatus"/>, and which of those becomes a 400 and which becomes a
    /// 502 is the transport layer's business.
    /// </remarks>
    public interface IProfileService
    {
        /// <summary>
        /// Reads the username, email and role of an account.
        /// </summary>
        /// <param name="userId">Identifier taken from the access token.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The profile, or null when the account is missing or is not
        /// <see cref="UserStatus.Registered"/> - a blocked account is not handed a profile.
        /// </returns>
        Task<AccountProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts changing the login name of an account: applies it at once when there is no
        /// mail server, otherwise stores it and mails the code that confirms it.
        /// </summary>
        /// <param name="userId">Identifier taken from the access token.</param>
        /// <param name="currentPassword">Password the account signs in with, verified here.</param>
        /// <param name="username">New login name, trimmed but otherwise untouched.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<ProfileChangeOutcome> ChangeUsernameAsync(
            Guid userId,
            string currentPassword,
            string username,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts changing the email address of an account. The code is mailed to the new
        /// address, because that is the address whose ownership the change has to prove.
        /// </summary>
        /// <param name="userId">Identifier taken from the access token.</param>
        /// <param name="currentPassword">Password the account signs in with, verified here.</param>
        /// <param name="email">New address, folded to lower case before anything is compared.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<ProfileChangeOutcome> ChangeEmailAsync(
            Guid userId,
            string currentPassword,
            string email,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts changing the password of an account. The new one is stored as a hash in the
        /// pending row and only replaces the account's own hash once the code is confirmed.
        /// </summary>
        /// <param name="userId">Identifier taken from the access token.</param>
        /// <param name="currentPassword">Password the account signs in with, verified here.</param>
        /// <param name="newPassword">Replacement password in clear text. Never stored as given.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<ProfileChangeOutcome> ChangePasswordAsync(
            Guid userId,
            string currentPassword,
            string newPassword,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies the change whose code was mailed to the account.
        /// </summary>
        /// <param name="userId">Identifier taken from the access token.</param>
        /// <param name="code">Code as typed, digits only.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The outcome, with <see cref="ProfileChangeOutcome.Account"/> set when the change went
        /// through. A wrong code and a missing pending row are reported identically.
        /// </returns>
        Task<ProfileChangeOutcome> ConfirmAsync(
            Guid userId,
            string code,
            CancellationToken cancellationToken = default);
    }
}
