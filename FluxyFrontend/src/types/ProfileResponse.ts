import type { Role } from './Role'

/** `GET /auth/profile` - the facts the profile page shows. */
export interface ProfileResponse {
  username: string
  email: string
  role: Role
  /**
   * IANA identifier of the chosen display time zone (`Europe/Moscow`), or null when the
   * visitor has not chosen one and the browser's own decides. Null is a normal state and
   * not a missing field: every account starts with it.
   */
  timeZone: string | null
}
