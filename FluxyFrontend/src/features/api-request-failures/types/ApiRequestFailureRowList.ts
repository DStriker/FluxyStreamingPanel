import type { ApiRequestFailureRow } from './ApiRequestFailureRow'

/**
 * `GET /admin/api-request-failures` - one page of the log, filtered and ordered by the
 * server.
 *
 * `total` is the size of the filtered list, counted under the same predicate the page is cut
 * from: that is what the pager is built from, and it is why every filter and the order are
 * answered by the server rather than over the page on screen.
 */
export interface ApiRequestFailureRowList {
  /** Records on this page, in the order asked for. */
  items: ApiRequestFailureRow[]
  /** How many records the same filter admits, all pages together. */
  total: number
  /** The page these records are, one based. */
  page: number
  /** How many records one page holds. */
  pageSize: number
}
