/**
 * One thing a group may let its members do, spelled exactly as the server's catalog spells it.
 *
 * These four strings are the whole catalog at the time of writing. They are a union rather
 * than `string` for the reason `errors` lands under camelCase JSON names on the other side:
 * a key typed here is checked where it is written, while a permission typed as `string` would
 * accept `viewUserss` and the server would answer `validation_failed` naming a field the form
 * never showed.
 *
 * A permission belongs to **exactly one role**, and on this installation that role is
 * `Admin` for all four - see `permissionsForRole` in `src/lib/permissions.ts`, which mirrors
 * `UserPermissionCatalog.ForRole` on the backend. A grant the group's role does not own is
 * dropped on the way in rather than refused, which is why a Client group sent all four comes
 * back with none: they would mean nothing, since the role gate ahead of them already says no.
 */
export type UserPermission = 'viewUsers' | 'editUsers' | 'viewUserGroups' | 'editUserGroups'
