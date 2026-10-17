import type { AdminSessionVisit } from './AdminSessionVisit'

/**
 * `GET /admin/sessions` - one page of the installation's visit history, newest first.
 *
 * The twin of `SessionHistoryResponse`, and the same shape for the same reason: `total`
 * counts every entry the caller may see rather than the rows of this page, because that is
 * what the pager draws itself from, and reading a count and a page separately would let the
 * two disagree while visits are still being written between them.
 *
 * What differs is whose rows they are. `total` is over the accounts this operator may read,
 * already narrowed by the server - a filter applied in the browser to the rows on screen
 * would narrow one page while this number still counted everything.
 */
export interface AdminSessionHistoryResponse {
  /** Visits on this page, newest first. */
  items: AdminSessionVisit[]
  /** How many visits the operator may see in total. */
  total: number
  /** The page these visits are, one based. */
  page: number
  /** How many visits one page holds. */
  pageSize: number
}
