import type { LoginGuardSettings } from './LoginGuardSettings'
import type { Role } from './Role'
import type { UserStatus } from './UserStatus'

/**
 * `GET /admin/users/{id}` - one account in full, for the edit form.
 *
 * The identifier is a string and not a field the form could submit: it is shown as static
 * text and travels back in the `PATCH` body's path, never as a property of the body - a
 * form that posted its own id as data would be one edit away from writing it.
 *
 * There is no password here and no pending code here. The first cannot be shown back and
 * the second is a credential, so both are absent rather than empty - which is also why the
 * password field of the form starts blank and means "keep the one that is there" whenever
 * it is still blank at submit time.
 *
 * The guard is nested under `loginGuard` rather than flattened across the body the way
 * `ProfileResponse` spells it: that body *is* the guard plus nothing, while this body is an
 * account and the guard is one section of its form. A field named `loginGuard.allowedIps`
 * cannot be confused with a field of the account itself.
 */
export interface AdminUserDetail {
  id: string
  username: string
  email: string
  role: Role
  status: UserStatus
  /** IANA identifier of the chosen display time zone, or null when the browser decides. */
  timeZone: string | null
  /** When the account confirmed its email address, or null while it never has. Blocking does not clear it. */
  registeredAt: string | null
  /** When the row was created. */
  createdAt: string
  /** When the account last signed in or refreshed, or null when it never has. */
  lastSeenAt: string | null
  /** Address that visit came from, or null when the row recorded none. */
  lastIp: string | null
  /** The two switches and the three allow lists of this account. */
  loginGuard: LoginGuardSettings
}
