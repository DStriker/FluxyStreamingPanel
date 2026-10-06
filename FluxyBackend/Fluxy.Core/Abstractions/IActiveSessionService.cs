using Fluxy.Core.Models.Authentication;

namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Reads the sessions an account currently holds, from the rows its own sessions already
    /// wrote.
    /// </summary>
    /// <remarks>
    /// Read only by construction: there is no method here that writes. Ending a session is
    /// <see cref="ITokenService"/>'s business - this interface describes what exists, and
    /// the controller decides which of it a caller may end. Rows appear when a token is
    /// issued and disappear when the session is revoked or the token expires; nothing this
    /// interface can do affects either.
    ///
    /// The interface says nothing about HTTP, JSON or status codes. It reports the account's
    /// sessions or nothing at all, and which of those becomes a 403 is the transport's
    /// business.
    /// </remarks>
    public interface IActiveSessionService
    {
        /// <summary>
        /// Reads every session the account currently holds, newest first.
        /// </summary>
        /// <param name="userId">Identifier taken from the access token.</param>
        /// <param name="currentSessionId">
        /// Session the caller is reading with, used to mark its own entry. Null when the token
        /// carried no session claim, which marks nothing rather than guessing.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The sessions, or null when the account is missing or is not registered - a blocked
        /// account is handed no list, the same rule the profile and history reads apply.
        /// </returns>
        Task<ActiveSessionList?> GetActiveAsync(
            Guid userId,
            Guid? currentSessionId,
            CancellationToken cancellationToken = default);
    }
}
