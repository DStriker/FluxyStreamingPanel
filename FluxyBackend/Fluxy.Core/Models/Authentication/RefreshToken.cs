using Fluxy.Core.Models.Users;

namespace Fluxy.Core.Models.Authentication
{
    /// <summary>
    /// One refresh token row, as the data layer reads and writes it.
    /// </summary>
    /// <remarks>
    /// The token itself is never on this type: the row stores a hash and nothing else, and the
    /// service that exchanges a presented token computes the same hash before asking for the
    /// row. <see cref="ReplacedById"/> is what makes a chain walkable; it is written when a
    /// token is spent, so a later presentation of the same row is recognisable as a replay.
    /// </remarks>
    public sealed record RefreshToken
    {
        /// <summary>Identifier of the row.</summary>
        public required Guid Id { get; init; }

        /// <summary>Account the token signs in.</summary>
        public required Guid UserId { get; init; }

        /// <summary>Session every token of one sign-in belongs to.</summary>
        public required Guid SessionId { get; init; }

        /// <summary>Lower case hexadecimal SHA-256 of the token.</summary>
        public required string TokenHash { get; init; }

        /// <summary>Moment the token stops being accepted.</summary>
        public required DateTimeOffset ExpiresAt { get; init; }

        /// <summary>Moment the token was spent or withdrawn, or null while it is live.</summary>
        public DateTimeOffset? RevokedAt { get; init; }

        /// <summary>Token issued in exchange for this one, or null while this one is live.</summary>
        public Guid? ReplacedById { get; init; }

        /// <summary>Address the sign-in or refresh came from.</summary>
        public string? ClientAddress { get; init; }

        /// <summary>User agent the sign-in or refresh came with.</summary>
        public string? UserAgent { get; init; }

        /// <summary>Moment the row was written.</summary>
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
