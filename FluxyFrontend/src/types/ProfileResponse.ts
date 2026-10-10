import type { Role } from './Role'
import type { UserPermission } from './UserPermission'

/** `GET /auth/profile` - the facts the profile page shows. */
export interface ProfileResponse {
  username: string
  email: string
  role: Role
  /**
   * Everything the visitor's group allows it to do, in the server's catalog order.
   *
   * Present because the account table has to know which of its own buttons are worth drawing
   * before a request is made - `viewAdmins` is not `viewUsers` any more, so "can this operator
   * see administrators" is a question about their group and not about the role in their token.
   * `GET /auth/me` deliberately does **not** carry it: the guest guard polls that on every
   * navigation for a yes-or-no answer, and this is one more row of the database behind it.
   *
   * A hint and never an authority - the server refuses on its own, in the service, so a client
   * that dropped this field entirely would draw buttons that do not work and gain nothing.
   */
  permissions: UserPermission[]
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
