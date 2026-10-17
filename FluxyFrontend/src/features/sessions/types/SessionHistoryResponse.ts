import type { SessionVisit } from './SessionVisit'

/**
 * `GET /auth/sessions` - one page of the account's visit history, newest first.
 *
 * `total` counts every entry the account has rather than the rows of this page, because that is
 * what the pager draws itself from. It is read in the same request as `items` on purpose: a
 * count and a page fetched separately can disagree while new visits are being written between
 * them, and a pager told 27 while holding a different 27 is worse than one that answers for the
 * moment it read.
 */
export interface SessionHistoryResponse {
  /** Visits on this page, newest first. */
  items: SessionVisit[]
  /** How many visits the account has in total. */
  total: number
  /** The page these visits are, one based. */
  page: number
  /** How many visits one page holds. */
  pageSize: number
}
