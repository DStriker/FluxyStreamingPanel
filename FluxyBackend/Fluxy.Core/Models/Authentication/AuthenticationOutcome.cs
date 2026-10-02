using Fluxy.Core.Models.Users;

namespace Fluxy.Core.Models.Authentication
{
    /// <summary>
    /// What a sign-in attempt produced.
    /// </summary>
    /// <remarks>
    /// Everything but <see cref="Status"/> describes a success. A refusal carries none of it,
    /// which is deliberate: a caller must not be able to read the role or the identifier of an
    /// account it failed to sign in to, and leaving them unset on the failure path is what makes
    /// that impossible rather than merely discouraged.
    /// </remarks>
    public sealed record AuthenticationOutcome
    {
        /// <summary>How the attempt ended.</summary>
        public required AuthenticationStatus Status { get; init; }

        /// <summary>Identifier of the account that was signed in. Only set on success.</summary>
        public Guid UserId { get; init; }

        /// <summary>Login name of the account. Only set on success.</summary>
        public string? Username { get; init; }

        /// <summary>Role of the account. Only set on success.</summary>
        public UserRole Role { get; init; } = UserRole.Client;

        /// <summary>Tokens issued for the session. Only set on success.</summary>
        public IssuedTokens? Tokens { get; init; }
    }
}
