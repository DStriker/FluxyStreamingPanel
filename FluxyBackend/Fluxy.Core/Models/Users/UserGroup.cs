namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// Immutable business representation of a group: which role its members inherit, which
    /// state they are in, and which permissions it grants.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A group is the place where "what level of access do these accounts have" (the role)
    /// and "what may they actually do here" (the permissions) meet, which is why an account
    /// stores a group and not a role: the two answers have to be changeable together, and a
    /// role column on <c>users</c> would be a second copy of a fact that lives here.
    /// </para>
    /// <para>
    /// <see cref="Role"/> and <see cref="Status"/> are the group's own values. What its
    /// members' <i>effective</i> status is depends on their rows too, and that combination
    /// belongs to <see cref="UserStatusComposition"/> rather than to this record.
    /// </para>
    /// </remarks>
    public sealed record UserGroup
    {
        /// <summary>Identifier of the group.</summary>
        public required Guid Id { get; init; }

        /// <summary>
        /// Display name, compared case sensitively. The one field every group may change -
        /// including the three the installation cannot lose.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Access level every member of this group holds. Every account is in exactly one
        /// group, so this is the account's own level by inheritance.
        /// </summary>
        public required UserRole Role { get; init; }

        /// <summary>
        /// State of the group itself. A member's own state combines with this one; see
        /// <see cref="UserStatusComposition"/>.
        /// </summary>
        public required UserStatus Status { get; init; }

        /// <summary>
        /// Everything this group allows, always a subset of what
        /// <see cref="UserPermissionCatalog.ForRole"/> offers for <see cref="Role"/>.
        /// </summary>
        public required IReadOnlySet<UserPermission> Permissions { get; init; }

        /// <summary>
        /// Whether this is one of the three groups that must exist under any circumstances.
        /// Derived from the identifier alone, so renaming cannot change it.
        /// </summary>
        public bool IsBase => BaseUserGroups.IsBase(Id);
    }
}
