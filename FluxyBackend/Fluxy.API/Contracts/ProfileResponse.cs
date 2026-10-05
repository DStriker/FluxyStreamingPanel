using Fluxy.Core.Models.Users;

namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What a profile page is shown about the account behind the presented token.
    /// </summary>
    /// <remarks>
    /// It exists because <see cref="SessionResponse"/> answers a different question: that one
    /// reports the claims of the token, and the email address is deliberately not one of them -
    /// a claim is a copy that lives for as long as the token does, and an address that can be
    /// changed must not be readable from a copy. This reads the row.
    /// </remarks>
    public sealed record ProfileResponse
    {
        /// <summary>Login name of the account.</summary>
        public required string Username { get; init; }

        /// <summary>Email address of the account.</summary>
        public required string Email { get; init; }

        /// <summary>Access level of the account, as the name of the enum member.</summary>
        public required string Role { get; init; }

        /// <summary>
        /// IANA identifier of the chosen display time zone, or null when the visitor has not
        /// chosen one and it should be read from their browser.
        /// </summary>
        public string? TimeZone { get; init; }
    }
}
