/**
 * One active sign-in session of the account: where it sits and when it was last seen.
 *
 * The frontend's copy of the server's `ActiveSessionResponse`. The GeoIP fields arrive already
 * resolved rather than as an address this page would look up itself - the database is server
 * side and the browser has no way to obtain it, so a null here means "the server did not know"
 * and not "this field is missing".
 *
 * A row is a *session* and not a token, which is the deliberate opposite of `SessionVisit`: the
 * history page lists every rotation so a network change is visible, this page lists what is
 * still signed in so a person can end what they do not recognise. `id` is the session, and
 * naming it is what lets `revokeActiveSession` end the whole chain by one value.
 */
export interface ActiveSession {
  /** Identifier of the session, unique per sign-in. Used as the card key. */
  id: string
  /** Address the session was last seen from, or `null` when the connection had none. */
  ip: string | null
  /** Country of the address, ISO 3166-1 alpha-2 upper case, or `null` when unknown. */
  countryCode: string | null
  /** Autonomous system number of the address, or `null` when unknown. */
  autonomousSystemNumber: number | null
  /** Organization the autonomous system is registered to, or `null` when unknown. */
  organization: string | null
  /** User agent of the freshest live token, or `null` when it carried none. */
  userAgent: string | null
  /** When the session was last seen - an ISO 8601 instant. */
  lastSeenAt: string
  /** Whether this session is the one the list is being read with. */
  isCurrent: boolean
}
