import type { ConnectionStreamKind } from './ConnectionStreamKind'

/**
 * One connection as `GET /admin/connection-logs` answers with it.
 *
 * The subscription and the stream arrive as identifiers *and* as the names the table draws,
 * because both are resolved by the server for the page it answered: the username from the
 * account that owns the subscription and the title from the stream table the kind names.
 * `streamTitle` is null when the stream row is gone - the session happened, the row it named
 * does not - and the cell draws a dash rather than a link into nothing.
 *
 * `countryCode` and `isp` are stored columns here, unlike the visit history's resolved ones:
 * the operator filters this table by both, so the value has to exist outside the request that
 * drew it. Either may be null, and the cell draws a dash.
 *
 * There is no duration: `startedAt` and `endedAt` are the two facts, and the reader subtracts
 * them - a stored duration would be a third value that could disagree with both.
 */
export interface ConnectionLog {
  /** Identifier of the connection, as text. The table shows it shortened. */
  id: string
  /** Identifier of the subscription it was made under, as text. */
  subscriptionId: string
  /** Identifier of the account that owns that subscription, as text - the account link's target. */
  ownerId: string
  /** Login name of that subscription's owner, resolved for the row. */
  username: string
  /** Which medium it delivered. */
  streamKind: ConnectionStreamKind
  /** Opaque identifier of the stream, as text. */
  streamId: string
  /** Name of that stream, or null when the row it named is gone. */
  streamTitle: string | null
  /** Address the session came from. */
  ip: string
  /** User agent the session presented, or null. */
  userAgent: string | null
  /** Upper-case ISO alpha-2 country of the address, or null when unknown. */
  countryCode: string | null
  /** Internet provider the address belongs to, or null when unknown. */
  isp: string | null
  /** When the session opened, as an ISO timestamp. */
  startedAt: string
  /** When it finished, or null while it is still live. */
  endedAt: string | null
  /** Process identifier the broadcasting side reported, or null. */
  broadcastPid: number | null
}
