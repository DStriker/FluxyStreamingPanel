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
    /// part. An account whose group holds <see cref="EditAdmins"/> but sits in a group with
    /// role <see cref="Users.UserRole.Client"/> still cannot reach the administrator's
    /// endpoints, because the role gate is checked first and separately.
    /// </para>
    /// <para>
    /// <b>The account permissions are split by the role of the account they act on, not by
    /// the role of the account doing the acting.</b> Every one of them is owned by
    /// <see cref="Users.UserRole.Admin"/> - they are questions asked inside the admin area -
    /// and what they differ on is which accounts of the installation they reach:
    /// <see cref="ViewClients"/> answers for rows whose group is a group of clients, while
    /// <see cref="ViewAdmins"/> answers for the administrators. That is what lets a group of
    /// operators run the clients and leave the other operators alone, which one pair of
    /// global keys could not express at all.
    /// </para>
    /// <para>
    /// The numeric values are powers of two and are stored in <c>user_group_permissions</c>,
    /// so they are part of the schema: adding a member means adding a value nobody has used
    /// yet, and renumbering the existing ones would silently reinterpret rows already
    /// written. The account keys therefore start at 16 rather than at 1 - the values 1 and 2
    /// were the retired <c>viewUsers</c>/<c>editUsers</c> of the first version, and handing
    /// them to a new meaning would turn every stored grant into a grant of something else
    /// without a single query being made. New permissions are added here and registered in
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

        /// <summary>May read the groups: the groups list and one group in full.</summary>
        ViewUserGroups = 4,

        /// <summary>May change the groups: create, rename, re-role, re-status and delete them.</summary>
        EditUserGroups = 8,

        /// <summary>
        /// May read the accounts of the installation whose group is a group of clients: the
        /// users list and one such account in full, and only those.
        /// </summary>
        ViewClients = 16,

        /// <summary>
        /// May read the accounts whose group is a group of resellers, and only those.
        /// </summary>
        ViewResellers = 32,

        /// <summary>
        /// May read the accounts whose group is a group of administrators, and only those.
        /// </summary>
        ViewAdmins = 64,

        /// <summary>
        /// May change the accounts of a group of clients: create them in such a group, edit,
        /// confirm, block, unblock and delete them. Separate from <see cref="ViewClients"/> so
        /// that a group which may watch the clients and a group which may run them are two
        /// different grants rather than one.
        /// </summary>
        EditClients = 128,

        /// <summary>May change the accounts of a group of resellers.</summary>
        EditResellers = 256,

        /// <summary>
        /// May change the accounts of a group of administrators - the highest grant in the
        /// system, and deliberately not implied by any of the others.
        /// </summary>
        EditAdmins = 512
    }
}
