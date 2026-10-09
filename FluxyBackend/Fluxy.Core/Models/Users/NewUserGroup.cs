namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// Everything an admin supplies to create a group in one request.
    /// </summary>
    /// <remarks>
    /// Permissions travel as a set rather than five booleans because that is what they are:
    /// one value the group either holds or does not, whose members grow as the system does.
    /// A permission of a role the group does not have is dropped rather than refused, so
    /// saving a group can never write a grant that means nothing.
    /// </remarks>
    public sealed record NewUserGroup
    {
        /// <summary>Name the group gets. Trimmed, compared case sensitively.</summary>
        public required string Name { get; init; }

        /// <summary>Access level every member of this group will hold.</summary>
        public required UserRole Role { get; init; }

        /// <summary>State the group starts in.</summary>
        public required UserStatus Status { get; init; }

        /// <summary>Permissions the group grants, filtered to what <see cref="Role"/> allows.</summary>
        public IReadOnlySet<UserPermission>? Permissions { get; init; }
    }

    /// <summary>
    /// What an admin changes about an existing group. Every property is optional, and an
    /// absent one means "leave it alone".
    /// </summary>
    /// <remarks>
    /// That is what makes one record serve the form and the rename a row offers, and it is
    /// also what stops a request from clearing a value simply because it forgot to include
    /// it. Two cases are the service's rather than the body's: a base group accepts
    /// <see cref="Name"/> and nothing else, and a role change re-checks every permission the
    /// group still holds.
    /// </remarks>
    public sealed record UserGroupPatch
    {
        /// <summary>New name, or null to keep the one on the row.</summary>
        public string? Name { get; init; }

        /// <summary>New access level for every member, or null to keep the current one.</summary>
        public UserRole? Role { get; init; }

        /// <summary>New state, or null to keep the current one.</summary>
        public UserStatus? Status { get; init; }

        /// <summary>
        /// Replacement permission set, or null to leave it alone. Sent whole: a set of which
        /// half the caller never speaks would be a set nobody can read.
        /// </summary>
        public IReadOnlySet<UserPermission>? Permissions { get; init; }
    }
}
