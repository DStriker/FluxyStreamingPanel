import type { ConnectionLog } from './ConnectionLog'

/**
 * `GET /admin/connection-logs` - one page of the log, filtered and ordered by the server.
 *
 * `total` is the size of the filtered list, counted before the page is cut: that is the whole
 * reason every filter and the order are answered by the server, and the reason a page turn
 * cannot show a row twice or skip one.
 */
export interface ConnectionLogList {
  /** Connections on this page, in the order asked for. */
  items: ConnectionLog[]
  /** How many connections the same filter admits, all pages together. */
  total: number
  /** The page these connections are, one based. */
  page: number
  /** How many connections one page holds. */
  pageSize: number
}
