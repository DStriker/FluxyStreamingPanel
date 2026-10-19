namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// The current values of one account, as the admin write flows need to see them before
    /// deciding what to change.
    /// </summary>
    /// <remarks>
    /// Deliberately narrower than <see cref="AdminUserDetail"/>: the write flows never draw a
    /// page, so the login guard rules and the freshest visit are two reads they would pay for
    /// and never look at. The group's own state travels because a block and an unblock both
    /// depend on whether the group is what blocks the account.
    /// </remarks>
    public sealed record AdminAccount
    {
        /// <summary>Identifier of the account.</summary>
        public required Guid Id { get; init; }

        /// <summary>Login name, at most 20 characters.</summary>
        public required string Username { get; init; }

        /// <summary>Email address of the owner, stored normalized.</summary>
        public required string Email { get; init; }

        /// <summary>State of the account's own row.</summary>
        public required UserStatus Status { get; init; }

        /// <summary>Moment the account confirmed its address, or null while it never has.</summary>
        public DateTimeOffset? RegisteredAt { get; init; }

        /// <summary>Group the account belongs to.</summary>
        public required Guid GroupId { get; init; }

        /// <summary>Display name of that group.</summary>
        public required string GroupName { get; init; }

        /// <summary>Access level inherited from that group.</summary>
        public required UserRole Role { get; init; }

        /// <summary>State of the group, which combines with the account's own.</summary>
        public required UserStatus GroupStatus { get; init; }
    }
}
