import type { Role } from './Role'
import type { UserStatus } from './UserStatus'

/**
 * One row of the admin group table: the six things a line of it draws.
 *
 * `GET /admin/user-groups` answers with these. Role and status travel as the name of the
 * enum member - `Admin`, `Blocked` - the same spelling the account table spells its own
 * pair, because that is what a client already matches on and a second spelling for one enum
 * would be a second fact about it.
 *
 * `permissionsCount` is a count rather than the set, for the same reason the column is: the
 * set is a question the *edit form* asks, and shipping it on every row of every page would
 * answer a question this table never puts. The set lives on `UserGroupDetail`.
 *
 * `isBase` is the flag the whole page is shaped by: the three groups the installation cannot
 * operate without may only be renamed, so their rows draw no delete button, and a form
 * loading one disables everything except the name. The server refuses the same moves with
 * `409 user_group_immutable` - the disabled control is the UI saying what the API would say,
 * not the only thing standing between an operator and the foundation rows.
 */
export interface UserGroup {
  /** Identifier of the group, as text. */
  id: string
  /** Display name, unique, compared case sensitively. */
  name: string
  /** Access level every member of this group inherits, as the name of the enum member. */
  role: Role
  /** State of the group itself, as the name of the enum member. */
  status: UserStatus
  /** How many permissions this group grants. */
  permissionsCount: number
  /** Whether this group may only be renamed. */
  isBase: boolean
}
