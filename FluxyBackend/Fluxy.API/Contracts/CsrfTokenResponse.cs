namespace Fluxy.API.Contracts
{
    /// <summary>
    /// The CSRF token a browser needs before it may post anything.
    /// </summary>
    /// <remarks>
    /// The same value is also written into a readable cookie by the endpoint, and a client is
    /// free to read it from either place. The cookie is the usual choice, because it survives a
    /// reload without another round trip; the body is what a client that cannot see the cookie -
    /// or that prefers to keep the token in memory - reads instead.
    /// </remarks>
    public sealed record CsrfTokenResponse
    {
        /// <summary>Token to send back in the request header the antiforgery filter reads.</summary>
        public required string Token { get; init; }
    }
}