using Fluxy.Core.Models.Users;

namespace Fluxy.Core.Models.Authentication
{
    /// <summary>
    /// What exchanging a refresh token produced.
    /// </summary>
    public sealed record RefreshOutcome
    {
        /// <summary>How the exchange ended.</summary>
        public required RefreshStatus Status { get; init; }

        /// <summary>The new pair. Only set when <see cref="Status"/> is <see cref="RefreshStatus.Refreshed"/>.</summary>
        public IssuedTokens? Tokens { get; init; }

        /// <summary>Role of the account the chain belongs to. Only set on success.</summary>
        public UserRole Role { get; init; } = UserRole.Client;

        /// <summary>Identifier of the account. Only set on success.</summary>
        public Guid UserId { get; init; }
    }
}
