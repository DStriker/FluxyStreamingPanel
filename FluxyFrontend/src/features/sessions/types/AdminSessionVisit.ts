/**
 * One entry of the installation's visit history, as an operator is shown it.
 *
 * The frontend's copy of the server's `AdminSessionVisitResponse`: `SessionVisit` plus the
 * two things the rest of the page needs and that the row cannot be asked for a second time -
 * the account the visit belongs to, and the session behind it.
 *
 * The GeoIP fields arrive already resolved rather than as an address this page would look
 * up itself, for the reason `SessionVisit` states: the resolver and its database are server
 * side and the browser has no way to obtain either, so a null here means "the server did
 * not know" and not "this field is missing".
 */
export interface AdminSessionVisit {
  /** Identifier of the history row, unique per token. Used as the row key. */
  id: string
  /**
   * Session this visit belongs to, which is what the row's button ends. Several rows of one
   * session carry the same value - each rotation is a visit, and all of them are one sign-in.
   */
  sessionId: string
  /** Identifier of the account the session belongs to, for the `?userId=` filter. */
  userId: string
  /** Username of that account, so a row does not have to be opened to read it. */
  username: string
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
  /**
   * Whether the session behind this row is still signed in and its button may do something.
   * False for a chain that was ended or quietly expired.
   */
  isActive: boolean
}
