using Fluxy.Core.Models.Users;

namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Replaces the password of an account for a visitor who is not signed in.
    /// </summary>
    /// <remarks>
    /// This is the endpoint that decides whether a password is reset, so it is deliberately
    /// shaped around what it has to prove: the visitor supplies a username and an email that
    /// belong to the same account, and the code that comes back proves control of that mailbox
    /// before anything is applied. Without the mail server there is nothing to prove it with,
    /// and the whole flow is refused rather than skipped - unlike a profile change, which can
    /// lean on the session and the current password.
    ///
    /// The new password travels in two steps: it is validated and hashed when the code is
    /// requested, held as a hash in the pending row, and only ever copied onto the account
    /// when the code matches. No clear text is stored anywhere, at any point.
    /// </remarks>
    public interface IPasswordResetService
    {
        /// <summary>Whether this installation can send a code at all.</summary>
        bool IsConfigured { get; }

        /// <summary>
        /// Checks the username and email pair, stores the requested password and mails the code
        /// that applies it.
        /// </summary>
        /// <param name="username">Login name of the account, compared case sensitively.</param>
        /// <param name="email">Address of the account, folded to lower case.</param>
        /// <param name="newPassword">Replacement password in clear text. Never stored as given.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<PasswordResetOutcome> RequestAsync(
            string username,
            string email,
            string newPassword,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies the password whose code was mailed out.
        /// </summary>
        /// <param name="username">Login name the reset was requested for.</param>
        /// <param name="code">Code as typed, digits only.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The outcome, with <see cref="PasswordResetOutcome.UserId"/> set when the password was
        /// replaced. A wrong code and a missing pending reset are reported identically.
        /// </returns>
        Task<PasswordResetOutcome> ConfirmAsync(
            string username,
            string code,
            CancellationToken cancellationToken = default);
    }
}
