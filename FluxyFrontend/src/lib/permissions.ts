import type { Role, UserPermission } from '../types'

/**
 * The frontend's copy of `UserPermissionCatalog` (`Fluxy.Core/Models/Users`).
 *
 * It exists for two jobs, and both are decisions taken *before* a request rather than
 * refusals explained after one. The first is the group form: which checkboxes it is entitled
 * to draw. The second is the account table and its form: which rows and which controls are
 * worth drawing for the signed-in operator. The server is still the authority - a grant its
 * role does not own is dropped on the way in rather than refused, and every endpoint refuses
 * again on its own - but a form that offered eight checkboxes for a Client group and then
 * silently threw away every tick would be a form that reported success for choices it never
 * made.
 *
 * Four rules are mirrored here rather than inferred, because each one is a fact only the
 * catalog states:
 *
 * - **A permission belongs to exactly one role.** On this installation all eight belong to
 *   `Admin`, so `permissionsForRole` answers an empty list for `Client` and `Reseller`. That
 *   is not a limitation of the table - a level decides which part of the API an account
 *   reaches at all, and a permission only answers a question asked *inside* that part. An
 *   operator who granted `editAdmins` to a group of clients would be granting something the
 *   role gate ahead of it already refuses.
 * - **A target role has exactly one view permission and exactly one edit permission**, so
 *   `canView` and `canEdit` are a lookup and not a scan over the set. This is the split that
 *   replaced `viewUsers`/`editUsers`: one permission per role *of the account being acted on*
 *   rather than one for accounts in general.
 * - **`ALL_PERMISSIONS` has a stable order**, so the checkboxes appear in the same sequence
 *   the server, the seeder and the migration list them in. Sorting them differently here
 *   would be a second opinion about an order that was decided once, on purpose.
 * - **Unknown is not a case.** Every value is a member of the union, so there is no branch
 *   for a name that means nothing - the server answers `validation_failed` with the list that
 *   does exist if a body ever carries one, and that is where the refusal belongs.
 */

/** Every permission the system knows, in the catalog's own order. */
export const ALL_PERMISSIONS: readonly UserPermission[] = [
  'viewClients',
  'editClients',
  'viewResellers',
  'editResellers',
  'viewAdmins',
  'editAdmins',
  'viewUserGroups',
  'editUserGroups',
]

/** The role that owns each permission. Every member of the catalog is owned by `Admin`. */
const OWNER: Readonly<Record<UserPermission, Role>> = {
  viewClients: 'Admin',
  editClients: 'Admin',
  viewResellers: 'Admin',
  editResellers: 'Admin',
  viewAdmins: 'Admin',
  editAdmins: 'Admin',
  viewUserGroups: 'Admin',
  editUserGroups: 'Admin',
}

/**
 * The permission that governs *reading accounts of* each role, mirroring
 * `UserPermissionCatalog.ViewFor`.
 */
const VIEW_FOR: Readonly<Record<Role, UserPermission>> = {
  Client: 'viewClients',
  Reseller: 'viewResellers',
  Admin: 'viewAdmins',
}

/**
 * The permission that governs *changing accounts of* each role, mirroring
 * `UserPermissionCatalog.EditFor`.
 */
const EDIT_FOR: Readonly<Record<Role, UserPermission>> = {
  Client: 'editClients',
  Reseller: 'editResellers',
  Admin: 'editAdmins',
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

/**
 * Whether the operator may *read* accounts of `role` - the question the row edit button and
 * the detail page ask.
 *
 * The account table does not have to gate its own rows: the server already filters the page
 * to the roles the operator may see, so every row that arrives has passed this test. What the
 * filter cannot do is reach the *form*, which is opened by address as often as by button -
 * and a detail page that answered 403 with no explanation ahead of it is the same gap.
 *
 * @param granted Everything the signed-in operator's group holds.
 * @param role Role of the account being looked at.
 */
export const canView = (granted: readonly UserPermission[], role: Role): boolean =>
  granted.includes(VIEW_FOR[role])

/**
 * Whether the operator may *change* accounts of `role` - the question every write button and
 * every group option on the account form asks.
 *
 * @param granted Everything the signed-in operator's group holds.
 * @param role Role of the account being changed, or of the group it would be moved into.
 */
export const canEdit = (granted: readonly UserPermission[], role: Role): boolean =>
  granted.includes(EDIT_FOR[role])

/**
 * Whether the operator may read *some* account at all, which is what the page needs before
 * any row has arrived. Mirrors the coarse `ViewAnyUsers` policy on the backend: the policy
 * settles "may this caller read accounts at all", and `canView` above settles which ones.
 */
export const canViewAny = (granted: readonly UserPermission[]): boolean =>
  (Object.keys(VIEW_FOR) as Role[]).some((role) => canView(granted, role))

/**
 * Whether the operator may change *some* account at all, which is what the "add" button and
 * the whole of the account form ask before a group has been chosen. Mirrors the coarse
 * `EditAnyUsers` policy on the backend.
 */
export const canEditAny = (granted: readonly UserPermission[]): boolean =>
  (Object.keys(EDIT_FOR) as Role[]).some((role) => canEdit(granted, role))
