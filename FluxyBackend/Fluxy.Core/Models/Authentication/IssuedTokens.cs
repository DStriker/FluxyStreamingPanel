namespace Fluxy.Core.Models.Authentication
{
    /// <summary>
    /// A freshly issued pair of tokens, and the session they belong to.
    /// </summary>
    /// <remarks>
    /// The two tokens are not peers. The access token is short lived and carries nothing the
    /// server looks at except its own signature; the refresh token is opaque, long lived, and
    /// exists in the database only as a hash. Ending a session therefore means the refresh
    /// chain, while the access token is limited by its own remaining lifetime.
    /// </remarks>
    public sealed record IssuedTokens
    {
        /// <summary>Signed token sent with every call. Lives for a few minutes.</summary>
        public required string AccessToken { get; init; }

        /// <summary>
        /// Opaque value exchanged for a new pair. Stored hashed, single use, and replaced by a
        /// fresh one on every refresh.
        /// </summary>
        public required string RefreshToken { get; init; }

        /// <summary>Moment the access token stops being accepted.</summary>
        public required DateTimeOffset AccessTokenExpiresAt { get; init; }

        /// <summary>Moment the refresh chain stops being accepted.</summary>
        public required DateTimeOffset RefreshTokenExpiresAt { get; init; }

        /// <summary>
        /// Identifier shared by every token in one refresh chain. Signing out revokes the
        /// whole chain, so a refresh token taken earlier in the session dies with it.
        /// </summary>
        public required Guid SessionId { get; init; }
    }
}
