namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// The four values of a group that another flow needs before it can decide what to write.
    /// </summary>
    /// <remarks>
    /// A projection rather than <see cref="UserGroup"/> because a caller choosing a group for
    /// an account asks only which role it holds, which state it is in and what it is called.
    /// Reading the permission set as well would be a second query for a value nothing in that
    /// decision reads.
    /// </remarks>
    public sealed record UserGroupSummary
    {
        /// <summary>Identifier of the group.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name, compared case sensitively.</summary>
        public required string Name { get; init; }

        /// <summary>Access level every member of this group holds.</summary>
        public required UserRole Role { get; init; }

        /// <summary>State of the group itself.</summary>
        public required UserStatus Status { get; init; }
    }
}
