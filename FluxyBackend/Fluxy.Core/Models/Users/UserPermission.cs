namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// One thing a group allows its members to do, as a flag so that "may this account do it"
    /// is a single bitwise test over the set its group carries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A permission is <i>not</i> a level of access. The level of access is
    /// <see cref="UserRole"/>, which decides what part of the API and of the interface an
    /// account reaches at all; a permission only answers a question asked <i>inside</i> that
    /// part. An account whose group holds <see cref="EditUsers"/> but sits in a group with
    /// role <see cref="Users.UserRole.Client"/> still cannot reach the administrator's
    /// endpoints, because the role gate is checked first and separately.
    /// </para>
    /// <para>
    /// The numeric values are powers of two and are stored in <c>user_group_permissions</c>,
    /// so they are part of the schema: adding a member means adding a value nobody has used
    /// yet, and renumbering the existing ones would silently reinterpret rows already
    /// written. New permissions are added here and registered in
    /// <see cref="UserPermissionCatalog"/>, which is what ties them to a role - no migration
    /// is needed for an addition, because the catalog is code and the base groups are
    /// reconciled with it at startup.
    /// </para>
    /// </remarks>
    [Flags]
    public enum UserPermission
    {
        /// <summary>No permission at all. Also the empty set a group may be saved with.</summary>
        None = 0,

        /// <summary>May read the accounts of the installation: the users list and one account in full.</summary>
        ViewUsers = 1,

        /// <summary>
        /// May change the accounts of the installation: create, edit, confirm, block, unblock
        /// and delete them. Implies <see cref="ViewUsers"/> in practice - an operator who can
        /// write a row can read it - but the two are separate keys so that a read-only group
        /// can exist without the write one.
        /// </summary>
        EditUsers = 2,

        /// <summary>May read the groups: the groups list and one group in full.</summary>
        ViewUserGroups = 4,

        /// <summary>May change the groups: create, rename, re-role, re-status and delete them.</summary>
        EditUserGroups = 8
    }
}
