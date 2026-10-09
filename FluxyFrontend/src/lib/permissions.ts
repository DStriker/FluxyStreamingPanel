import type { Role, UserPermission } from '../types'

/**
 * The frontend's copy of `UserPermissionCatalog` (`Fluxy.Core/Models/Users`).
 *
 * It exists for one job: deciding which checkboxes a group form is entitled to draw *before*
 * anything is sent. The server is still the authority - a grant its role does not own is
 * dropped on the way in rather than refused - but a form that offered four checkboxes for a
 * Client group and then silently threw away every tick would be a form that reported success
 * for choices it never made.
 *
 * Three rules are mirrored here rather than inferred, because each one is a fact only the
 * catalog states:
 *
 * - **A permission belongs to exactly one role.** On this installation all four belong to
 *   `Admin`, so `permissionsForRole` answers an empty list for `Client` and `Reseller`. That
 *   is not a limitation of the table - a level decides which part of the API an account
 *   reaches at all, and a permission only answers a question asked *inside* that part. An
 *   operator who granted `editUsers` to a group of clients would be granting something the
 *   role gate ahead of it already refuses.
 * - **`All` has a stable order**, so the checkboxes appear in the same sequence the server,
 *   the seeder and the migration list them in. Sorting them differently here would be a
 *   second opinion about an order that was decided once, on purpose.
 * - **Unknown is not a case.** Every value is a member of the union, so there is no branch
 *   for a name that means nothing - the server answers `validation_failed` with the list that
 *   does exist if a body ever carries one, and that is where the refusal belongs.
 */

/** Every permission the system knows, in the catalog's own order. */
export const ALL_PERMISSIONS: readonly UserPermission[] = [
  'viewUsers',
  'editUsers',
  'viewUserGroups',
  'editUserGroups',
]

/** The role that owns each permission. Every member of the catalog is owned by `Admin`. */
const OWNER: Readonly<Record<UserPermission, Role>> = {
  viewUsers: 'Admin',
  editUsers: 'Admin',
  viewUserGroups: 'Admin',
  editUserGroups: 'Admin',
}

/**
 * The permissions a group of the given role may hold - which is by definition the set its
 * base group carries.
 *
 * @param role Level the group grants to its members.
 * @returns The permissions of that role in catalog order, possibly empty.
 */
export const permissionsForRole = (role: Role): UserPermission[] =>
  ALL_PERMISSIONS.filter((permission) => OWNER[permission] === role)
