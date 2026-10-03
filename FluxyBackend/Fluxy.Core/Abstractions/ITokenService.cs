using Fluxy.Core.Models.Authentication;
using Fluxy.Core.Models.Users;

namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Mints, rotates and ends the tokens of a signed-in account.
    /// </summary>
    /// <remarks>
    /// The interface is deliberately about tokens rather than about people. It never asks who is
    /// signing in - that is the authentication service's job - and it never checks a password.
    /// What it owns is the rule that a session is a chain: one refresh token at a time, each one
    /// single use, all of them belonging to one session that can be ended as a whole.
    /// </remarks>
    public interface ITokenService
    {
        /// <summary>
        /// Opens a session for an account and issues the first pair of tokens.
        /// </summary>
        /// <param name="user">Account to sign in. Its role goes into the access token.</param>
        /// <param name="clientAddress">Address the sign-in came from, recorded on the session.</param>
        /// <param name="userAgent">User agent the sign-in came with, recorded on the session.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The issued pair, including the session they belong to.</returns>
        Task<IssuedTokens> IssueAsync(
            User user,
            string? clientAddress = null,
            string? userAgent = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Exchanges a refresh token for a new pair, revoking the one that was presented.
        /// </summary>
        /// <param name="presentedToken">Refresh token the client sent.</param>
        /// <param name="clientAddress">Address the refresh came from.</param>
        /// <param name="userAgent">User agent the refresh came with.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// How the exchange ended. <see cref="RefreshStatus.SessionRevoked"/> means the token
        /// had already been spent, and the whole chain was destroyed in response.
        /// </returns>
        Task<RefreshOutcome> RefreshAsync(
            string? presentedToken,
            string? clientAddress = null,
            string? userAgent = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Ends a session, so that no refresh token in its chain can be exchanged again.
        /// </summary>
        /// <param name="sessionId">Session to end, as carried by the access token.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>True when at least one live token was revoked.</returns>
        /// <remarks>
        /// This covers the refresh chain only. An access token that is already in a browser
        /// keeps working until it expires, which is what
        /// <see cref="ITokenRevocationStore"/> is for.
        /// </remarks>
        Task<bool> RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Ends every session of one account, so that no refresh token it holds can be
        /// exchanged again.
        /// </summary>
        /// <param name="userId">Account whose sessions end.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>True when at least one live token was revoked.</returns>
        /// <remarks>
        /// This is the security answer to a password that changed: whoever held the old
        /// password may hold a session with it, and the only honest reading of that is to assume
        /// they do. The access tokens already in browsers still run until they expire, which is
        /// bounded by <c>Auth:AccessLifetime</c> - the refresh chain is what this ends, and it
        /// is what keeps a session alive.
        /// </remarks>
        Task<bool> RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
