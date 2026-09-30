using Fluxy.Core.Models.Users;

namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Takes registration requests, stores the account and issues the one-time code that
    /// confirms it.
    /// </summary>
    public interface IRegistrationService
    {
        /// <summary>
        /// Registers a new account and sends its registration code to the supplied address.
        /// </summary>
        /// <remarks>
        /// The call is idempotent in the sense that a repeated request for an address that
        /// never finished registering issues a fresh code rather than failing, so a lost
        /// message does not lock the visitor out. An address that is already
        /// <see cref="UserStatus.Registered"/> or <see cref="UserStatus.Blocked"/> is never
        /// touched and never mailed.
        /// </remarks>
        /// <param name="registration">Values the visitor supplied, before normalization.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>How the request ended, including whether a row was written.</returns>
        Task<RegistrationOutcome> RegisterAsync(
            NewRegistration registration,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Confirms a pending registration with the code that was mailed to the address.
        /// </summary>
        /// <remarks>
        /// The code is stored hashed, so confirming it costs the same deliberately expensive
        /// comparison that producing it did. Callers are expected to have limited how often a
        /// single client may reach this method.
        /// </remarks>
        /// <param name="email">Address the account was registered with.</param>
        /// <param name="code">Code that was mailed to it.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>How the request ended. No row is ever written by this call.</returns>
        Task<RegistrationOutcome> ConfirmAsync(
            string email,
            string code,
            CancellationToken cancellationToken = default);
    }
}
