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
  /**
   * Whether a sign-in from a network that is not allowed is refused with the same answer
   * a wrong password gets. Off means the lists below are stored but ignored.
   */
  geoProtectionEnabled: boolean
  /** Whether a refresh from another address ends the session instead of rotating it. */
  bindSessionToIp: boolean
  /** Allowed addresses, exact or CIDR. Empty means this list does not restrict. */
  allowedIps: string[]
  /** Allowed country, ISO 3166-1 alpha-2, or null when not restricted. */
  allowedCountry: string | null
  /** Allowed provider by autonomous system number, or null when not restricted. */
  allowedAutonomousSystemNumber: number | null
}
