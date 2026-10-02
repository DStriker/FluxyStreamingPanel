namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Remembers access tokens that were withdrawn before their time was up, so that signing
    /// out takes effect at once instead of at the end of the token's life.
    /// </summary>
    /// <remarks>
    /// A signed token is self contained: nothing about it can be changed after it is written,
    /// and that is exactly what makes it cheap to accept and also why it cannot be un-issued.
    /// The only part that can be withdrawn is the token's own identifier, and only while the
    /// token would still have been accepted anyway.
    ///
    /// That is why the two operations take and return a lifetime. An entry is worth keeping for
    /// precisely as long as the token it names would have worked, so the store is empty again by
    /// itself and never needs a cleanup pass.
    /// </remarks>
    public interface ITokenRevocationStore
    {
        /// <summary>
        /// Marks a token identifier as withdrawn for the given time.
        /// </summary>
        /// <param name="tokenId">
        /// Identifier of the access token, the <c>jti</c> claim. An absent or empty value is
        /// ignored, because a token without one could not be named here later either.
        /// </param>
        /// <param name="lifetime">
        /// How long the withdrawal has to outlive the token. Anything at or below zero means the
        /// token has already expired and there is nothing to remember.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task BlockAsync(string? tokenId, TimeSpan lifetime, CancellationToken cancellationToken = default);

        /// <summary>
        /// Whether a token identifier has been withdrawn.
        /// </summary>
        /// <param name="tokenId">Identifier of the access token to look for.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// True when the token must be refused. False when it is absent from the store, and also
        /// when the store itself could not be reached: an unreachable store is a degraded
        /// installation, and refusing every request because of it would be a worse outcome than
        /// the short window it leaves open.
        /// </returns>
        Task<bool> IsBlockedAsync(string? tokenId, CancellationToken cancellationToken = default);
    }
}
