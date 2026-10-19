namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Remembers sessions whose access tokens were withdrawn before their time was up, so
    /// that ending a session takes effect at once instead of at the end of the token's life.
    /// </summary>
    /// <remarks>
    /// A signed token is self contained: nothing about it can be changed after it is written,
    /// and that is exactly what makes it cheap to accept and also why it cannot be un-issued.
    /// The one fact the token carries that can be remembered afterwards is the session it
    /// belongs to, and every access token of one session shares it.
    ///
    /// That is why the two operations take and return a lifetime. An entry is worth keeping
    /// for precisely as long as the longest lived access token of the session could still
    /// have worked, so the store is empty again by itself and never needs a cleanup pass.
    /// </remarks>
    public interface ITokenRevocationStore
    {
        /// <summary>
        /// Marks every access token of a session as withdrawn for the given time.
        /// </summary>
        /// <param name="sessionId">
        /// Session the access tokens belong to, the <c>sid</c> claim. An absent or empty value
        /// is ignored, because a token without one could not be named here later either.
        /// </param>
        /// <param name="lifetime">
        /// How long the withdrawal has to outlive every access token the session could still
        /// present. Anything at or below zero means no such token is left and there is nothing
        /// to remember.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task BlockSessionAsync(
            Guid? sessionId,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Whether the access tokens of a session have been withdrawn.
        /// </summary>
        /// <param name="sessionId">Session the presented access token belongs to.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// True when every access token of the session must be refused. False when it is absent
        /// from the store, and also when the store itself could not be reached: an unreachable
        /// store is a degraded installation, and refusing every request because of it would be
        /// a worse outcome than the short window it leaves open.
        /// </returns>
        Task<bool> IsSessionBlockedAsync(
            Guid? sessionId,
            CancellationToken cancellationToken = default);
    }
}
