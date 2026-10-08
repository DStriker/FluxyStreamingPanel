import type { Role } from './Role'
import type { UserStatus } from './UserStatus'

/**
 * One row of the admin's user table: the seven things a line of it draws.
 *
 * `GET /admin/users` answers with these. Role and status travel as the name of the enum
 * member - `Admin`, `Blocked` - the same spelling the profile already answers with, because
 * that is what every key on this side is written against and a second spelling for one enum
 * would be a second fact about it.
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
  /** Access level, as the name of the enum member. */
  role: Role
  /** State of the account, as the name of the enum member. */
  status: UserStatus
  /** When the account last signed in or refreshed, or null when it never has. */
  lastSeenAt: string | null
  /** Address that visit came from, or null when the row recorded none. */
  lastIp: string | null
}
