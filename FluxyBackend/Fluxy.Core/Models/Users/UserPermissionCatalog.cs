namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// Every permission the system knows, and which role each one belongs to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the single place a permission is named, matched and scoped. Three things read
    /// it: the startup seeder (which keeps the three base groups holding every permission of
    /// their own role, so a permission added later arrives without a migration), the group
    /// service (which refuses a permission that does not belong to the group's role), and the
    /// API (which turns the <c>"viewUsers"</c> strings a body carries into members and back).
    /// </para>
    /// <para>
    /// <b>A permission belongs to exactly one role.</b> That is what makes "the base group
    /// holds the full list of permissions <i>for its role</i>" a definition rather than a
    /// coincidence, and what stops an operator from granting <see cref="EditUsers"/> to a
    /// group of clients: the member would be accepted and then mean nothing, because the role
    /// gate ahead of it already refused. A permission whose owner role changes is therefore a
    /// change to this table and nothing else - the storage does not care.
    /// </para>
    /// </remarks>
    public static class UserPermissionCatalog
    {
        /// <summary>
        /// The whole catalog, in a stable order so a form, a log line and a migration can
        /// agree on how the permissions are listed.
        /// </summary>
        public static IReadOnlyList<UserPermission> All { get; } =
        [
            UserPermission.ViewUsers,
            UserPermission.EditUsers,
            UserPermission.ViewUserGroups,
            UserPermission.EditUserGroups
        ];

        /// <summary>
        /// The role that owns each permission. Only the members named here may exist; a flag
        /// that is not a key of this table is not a permission of this system.
        /// </summary>
        private static readonly IReadOnlyDictionary<UserPermission, UserRole> Owner = new
            Dictionary<UserPermission, UserRole>
            {
                [UserPermission.ViewUsers] = UserRole.Admin,
                [UserPermission.EditUsers] = UserRole.Admin,
                [UserPermission.ViewUserGroups] = UserRole.Admin,
                [UserPermission.EditUserGroups] = UserRole.Admin
            };

        /// <summary>
        /// Every permission of one role, which is by definition the set the base group of
        /// that role holds.
        /// </summary>
        /// <param name="role">Role whose permissions are wanted.</param>
        /// <returns>The permissions in <see cref="All"/> order, possibly empty.</returns>
        public static IReadOnlyList<UserPermission> ForRole(UserRole role)
        {
            var result = new List<UserPermission>(All.Count);

            foreach (var permission in All)
            {
                if (Owner.TryGetValue(permission, out var owner) && owner == role)
                {
                    result.Add(permission);
                }
            }

            return result;
        }

        /// <summary>
        /// Whether a permission exists at all in this system, whatever the role asked for.
        /// </summary>
        /// <param name="permission">Value to test, which may be a combination of flags.</param>
        /// <returns>
        /// True when every set bit is a member of the catalog; false for an unknown flag.
        /// </returns>
        public static bool IsKnown(UserPermission permission)
        {
            // None is the identity of the set and is always acceptable: a group with no
            // permissions is a group that grants nothing, which is a fact rather than an error.
            var remaining = permission & ~UserPermission.None;

            foreach (var member in All)
            {
                remaining &= ~member;
            }

            return remaining == UserPermission.None;
        }

        /// <summary>
        /// Whether a permission is offered to a group of the given role.
        /// </summary>
        /// <param name="permission">Single permission to test.</param>
        /// <param name="role">Role of the group it would be granted to.</param>
        /// <returns>True when the permission exists and is owned by <paramref name="role"/>.</returns>
        public static bool IsValidForRole(UserPermission permission, UserRole role)
        {
            return Owner.TryGetValue(permission, out var owner) && owner == role;
        }

        /// <summary>
        /// Drops every requested permission the role may not hold, so that saving a group
        /// never writes a grant that means nothing.
        /// </summary>
        /// <param name="requested">What the caller asked for.</param>
        /// <param name="role">Role of the group being saved.</param>
        /// <returns>The subset of <paramref name="requested"/> valid for <paramref name="role"/>.</returns>
        public static UserPermission Normalise(IEnumerable<UserPermission> requested, UserRole role)
        {
            var result = UserPermission.None;

            foreach (var permission in requested)
            {
                if (IsValidForRole(permission, role))
                {
                    result |= permission;
                }
            }

            return result;
        }

        /// <summary>
        /// The name a client sends and reads this permission by: the member name with a
        /// lower-case first letter, spelled exactly as the JSON contracts spell every other
        /// field.
        /// </summary>
        /// <param name="permission">Member to name.</param>
        /// <returns><c>viewUsers</c> for <see cref="UserPermission.ViewUsers"/>, and so on.</returns>
        public static string NameOf(UserPermission permission)
        {
            var text = permission.ToString();

            return text.Length is 0 or 1
                ? text
                : char.ToLowerInvariant(text[0]) + text[1..];
        }

        /// <summary>
        /// Reads a permission back from the name a client sent. Case insensitive, because a
        /// caller spelling <c>ViewUsers</c> is sending the enum member it was told exists,
        /// not a different permission.
        /// </summary>
        /// <param name="value">Name as sent, possibly null.</param>
        /// <param name="permission">The member the name stands for, when there is one.</param>
        /// <returns>True when the name names a member of the catalog.</returns>
        public static bool TryParse(string? value, out UserPermission permission)
        {
            permission = UserPermission.None;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (var member in All)
            {
                if (string.Equals(NameOf(member), value.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    permission = member;

                    return true;
                }
            }

            return false;
        }

        /// <summary>The sentence a permission that names no member gets.</summary>
        public const string UnknownPermissionHint =
            "Permission must be one of: viewUsers, editUsers, viewUserGroups, editUserGroups.";
    }
}
