import type { Role } from './Role'
import type { UserStatus } from './UserStatus'
import type { UserPermission } from './UserPermission'

/**
 * `GET /admin/user-groups/{id}` - one group in full, for the edit form.
 *
 * The identifier is a string and not a field the form could submit: it is shown as static
 * text and travels back in the `PATCH` path, never as a property of the body - a form that
 * posted its own id as data would be one edit away from writing it.
 *
 * `permissions` is the individual keys rather than a bitmask number. A client that had to
 * decode `15` would be one that has to know which bit the system set next, which is the
 * thing a permission catalog exists to keep stable. They arrive in the catalog's own order,
 * so the same group draws the same list on every visit.
 *
 * `members` is a count of accounts that name this group, not a list of them: it is what lets
 * the delete button explain itself *before* it is pressed rather than being refused
 * afterwards with `409 user_group_in_use`, and the accounts themselves belong to the page
 * that already exists for them.
 *
 * `createdAt` and `updatedAt` are both here although only the second says anything about the
 * row's history - the pair is what every auditable row on this installation carries, and a
 * detail that dropped one would be a detail that disagreed with the database.
 */
export interface UserGroupDetail {
  /** Identifier of the group, as text. */
  id: string
  /** Display name, unique, compared case sensitively. */
  name: string
  /** Access level every member inherits, as the name of the enum member. */
  role: Role
  /** State of the group itself, as the name of the enum member. */
  status: UserStatus
  /** Every permission this group grants, in the catalog's own order. */
  permissions: UserPermission[]
  /** Whether this group may only be renamed. */
  isBase: boolean
  /** How many accounts belong to this group right now. */
  members: number
  /** When the row was created. */
  createdAt: string
  /** When the row was last changed. */
  updatedAt: string
}
