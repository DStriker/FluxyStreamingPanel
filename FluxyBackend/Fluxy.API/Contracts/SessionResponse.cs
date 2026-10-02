using Fluxy.Core.Models.Users;

namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What a client is told about the session it already holds.
    /// </summary>
    /// <remarks>
    /// It exists because the tokens are not readable by the browser. A token in an
    /// <c>HttpOnly</c> cookie is deliberately invisible to the page that received it, so the
    /// frontend cannot ask the cookie who the user is, when it expires or what role it holds -
    /// it has to ask the server, and this is the answer. The alternative, reading the token in
    /// JavaScript, would undo the whole point of keeping it out of reach of scripts.
    /// </remarks>
    public sealed record SessionResponse
    {
        /// <summary>Identifier of the account, as a string so the browser need not parse a GUID.</summary>
        public string? UserId { get; init; }

        /// <summary>Login name of the account.</summary>
        public string? Username { get; init; }

        /// <summary>
        /// Role the account holds, spelled as it appears in the protocol. The client branches on
        /// this to choose which landing page to show, which is why it is a name rather than the
        /// numeric value: a client that guessed at numbering would be wrong the first time a
        /// role is inserted.
        /// </summary>
        public string Role { get; init; } = nameof(UserRole.Client);

        /// <summary>Whether the session is still usable, for the case where it is not.</summary>
        /// <remarks>
        /// Null on a successful answer and set on a refusal, so one shape covers both and a
        /// client never has to read a code off an error body to learn why it is looking at this.
        /// </remarks>
        public string? Code { get; init; }
    }
}