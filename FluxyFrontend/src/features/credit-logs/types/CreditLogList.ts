import type { CreditLog } from './CreditLog'

/**
 * `GET /admin/credit-logs` - one page of the log, filtered and ordered by the server.
 *
 * `total` is the size of the filtered list, counted before the page is cut under the same
 * predicate: that is what the pager is built from, and it is why every filter and the order
 * are answered by the server rather than over the page on screen.
 */
export interface CreditLogList {
  /** Entries on this page, in the order asked for. */
  items: CreditLog[]
  /** How many entries the same filter admits, all pages together. */
  total: number
  /** The page these entries are, one based. */
  page: number
  /** How many entries one page holds. */
  pageSize: number
}
