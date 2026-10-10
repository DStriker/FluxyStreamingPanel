/**
 * One thing a group may let its members do, spelled exactly as the server's catalog spells it.
 *
 * These eight strings are the whole catalog at the time of writing. They are a union rather
 * than `string` for the reason `errors` lands under camelCase JSON names on the other side:
 * a key typed here is checked where it is written, while a permission typed as `string` would
 * accept `viewClientss` and the server would answer `validation_failed` naming a field the form
 * never showed.
 *
 * **The four `*Users` permissions were replaced by one per target role.** `viewUsers` and
 * `editUsers` said "may this group touch accounts at all", which is a question about no
 * particular account - every page here draws rows of a *known* role, so a group allowed to run
 * the clients could still open, patch and delete an administrator. The replacement is a pair
 * per role: `viewClients`/`editClients`, `viewResellers`/`editResellers`,
 * `viewAdmins`/`editAdmins`. The two groups permissions are unchanged, because "may read the
 * group table" is not a claim about any particular group.
 *
 * A permission belongs to **exactly one role**, and on this installation that role is
 * `Admin` for all eight - see `permissionsForRole` in `src/lib/permissions.ts`, which mirrors
 * `UserPermissionCatalog.ForRole` on the backend. A grant the group's role does not own is
 * dropped on the way in rather than refused, which is why a Client group sent all eight comes
 * back with none: they would mean nothing, since the role gate ahead of them already says no.
 *
 * Note the two questions are spelled the same way and are not the same question:
 * `permissionsForRole(role)` takes the role of the group *holding* the grant, while
 * `canView(granted, role)` takes the role of the account being *looked at*.
 */
export type UserPermission =
  | 'viewClients'
  | 'editClients'
  | 'viewResellers'
  | 'editResellers'
  | 'viewAdmins'
  | 'editAdmins'
  | 'viewUserGroups'
  | 'editUserGroups'
