/**
 * One entry of the account's visit history: where a session came from and when.
 *
 * The frontend's copy of the server's `SessionVisitResponse`. The GeoIP fields arrive already
 * resolved rather than as an address this page would look up itself - the database is server
 * side and the browser has no way to obtain it, so a null here means "the server did not know"
 * and not "this field is missing".
 */
export interface SessionVisit {
  /** Identifier of the history row, unique per token. Used as the row key. */
  id: string
  /** Address the request arrived from, or `null` when the connection had none. */
  ip: string | null
  /** Country of the address, ISO 3166-1 alpha-2 upper case, or `null` when unknown. */
  countryCode: string | null
  /** Autonomous system number of the address, or `null` when unknown. */
  autonomousSystemNumber: number | null
  /** Organization the autonomous system is registered to, or `null` when unknown. */
  organization: string | null
  /** User agent the request carried, or `null` when it carried none. */
  userAgent: string | null
  /** When the token was issued or rotated - an ISO 8601 instant. */
  visitedAt: string
  /** Whether this entry belongs to the session the list is being read with. */
  isCurrent: boolean
}
