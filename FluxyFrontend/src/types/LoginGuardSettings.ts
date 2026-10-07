/**
 * `GET /auth/profile` renders this beside the account itself, and the `loginGuard` field of
 * `PATCH /auth/profile` accepts it back: the two switches and the three allow lists.
 *
 * The spelling is the camelCase of the server's names, for the reason every contract here
 * follows it - one fact with two spellings is a typo waiting to be read as a missing
 * field. An empty list does not restrict; only the switch decides whether the lists are
 * read at all.
 */
export interface LoginGuardSettings {
  geoProtectionEnabled: boolean
  bindSessionToIp: boolean
  /** Allowed addresses, exact or CIDR. At most five - the server refuses a sixth. */
  allowedIps: string[]
  /** Allowed country, ISO 3166-1 alpha-2 upper case, or null when not restricted. */
  allowedCountry: string | null
  /** Allowed provider by autonomous system number, or null when not restricted. */
  allowedAutonomousSystemNumber: number | null
}
