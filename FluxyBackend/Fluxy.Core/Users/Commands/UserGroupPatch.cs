using Fluxy.Core.Users.Enums;

namespace Fluxy.Core.Users.Commands
{
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
