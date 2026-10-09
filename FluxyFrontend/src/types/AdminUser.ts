import type { Role } from './Role'
import type { UserStatus } from './UserStatus'

/**
 * One row of the admin's user table: the ten things a line of it draws.
 *
 * `GET /admin/users` answers with these. Role and status travel as the name of the enum
 * member - `Admin`, `Blocked` - the same spelling the profile already answers with, because
 * that is what every key on this side is written against and a second spelling for one enum
 * would be a second fact about it.
 *
 * The account no longer *has* a level of its own: `role` is what its group holds, and
 * `groupName` is the column the table draws. Both travel because both are read - the name
 * down the table, the level whenever a page needs to know which area this account may reach
 * - but neither is a field the edit form writes. The form writes `groupId`, and the level
 * follows from wherever that lands.
 *
 * `status` is the **effective** state of the account: its own row combined with its group's
 * under `UserStatusComposition`, the most restrictive of the two winning. So a row reading
 * `Blocked` here is blocked whether or not anybody pressed block on *it* - the other way it
 * could be blocked is its group, and a page that showed only the stored value would show a
 * green account nobody can sign in to. `GET /admin/users/{id}` answers with that same value
 * as `status` and with the stored one beside it as `effectiveStatus`; the list has nowhere
 * to put two, so it shows the one that is true.
 *
 * `lastSeenAt` and `lastIp` are one visit, not two columns: the address belongs to that
 * moment, and both are null for an account that has never signed in. The server resolves
 * them from the freshest refresh token, so the table never reconstructs a visit from rows
 * it was not given.
 */
export interface AdminUser {
  /** Identifier of the account, as text. The table shows it shortened and keeps the full value in the tooltip. */
  id: string
  /** Login name, at most 20 characters. Unique, compared case sensitively. */
  username: string
  /** Email address of the owner, stored normalized. */
  email: string
  /** Access level the account inherits from its group, as the name of the enum member. */
  role: Role
  /** Identifier of the group that supplies the level, as text. */
  groupId: string
  /** Display name of that group, which is what the column draws. */
  groupName: string
  /** Effective state of the account - its own row and its group's, most restrictive first. */
  status: UserStatus
  /** When the account last signed in or refreshed, or null when it never has. */
  lastSeenAt: string | null
  /** Address that visit came from, or null when the row recorded none. */
  lastIp: string | null
}
