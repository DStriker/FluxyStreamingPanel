namespace Fluxy.Core.Models.Authentication
{
    /// <summary>
    /// How an attempt to exchange a refresh token for a new pair ended.
    /// </summary>
    /// <remarks>
    /// The three members map onto three different situations rather than three flavours of
    /// failure. <see cref="SessionRevoked"/> is separated from <see cref="InvalidToken"/> on
    /// purpose: presenting a refresh token that was already spent is the signature of a token
    /// that was copied and used twice, and the only safe reading of that is that the copy is in
    /// use by somebody, so the whole chain is destroyed. A caller that simply lost its cookie
    /// gets the chain revoked too, and the cost is one sign-in.
    /// </remarks>
    public enum RefreshStatus
    {
        /// <summary>The token was valid and a new pair has been issued in its place.</summary>
        Refreshed = 0,

        /// <summary>
        /// The token was not recognised, had already expired, or belonged to no live chain. No
        /// new pair was issued and nothing else was changed.
        /// </summary>
        InvalidToken = 1,

        /// <summary>
        /// The token had already been spent, so the chain it belonged to was treated as
        /// compromised and revoked in full.
        /// </summary>
        SessionRevoked = 2
    }
}
