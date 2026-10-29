using Fluxy.Core.Users.Enums;

namespace Fluxy.Core.Subscriptions.Rows
{
    /// <summary>
    /// The account a subscription is about, as one save needs it: who it is, the group whose
    /// role and state it inherits, and whether that makes it blocked.
    /// </summary>
    /// <remarks>
    /// A projection shaped for one query, and nothing else - it never reaches a response. The
    /// role is here because the issuer may only be reassigned to an administrator or a reseller,
    /// and the owner may be anybody; the blocked flag is the composed answer rather than the
    /// row's own, for the reason <c>UserStatusComposition</c> gives.
    /// </remarks>
    public sealed record SubscriptionOwnerContext
    {
        /// <summary>Identifier of the account.</summary>
        public required Guid Id { get; init; }

        /// <summary>Login name, which the list and the detail draw.</summary>
        public required string Username { get; init; }

        /// <summary>Group the account belongs to.</summary>
        public required Guid GroupId { get; init; }

        /// <summary>Level the account inherits from that group.</summary>
        public required UserRole Role { get; init; }

        /// <summary>Whether the account's effective state is Blocked.</summary>
        public required bool Blocked { get; init; }
    }
}
