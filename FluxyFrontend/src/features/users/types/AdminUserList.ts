import type { AdminUser } from './AdminUser'

/**
 * `GET /admin/users` - one page of the accounts, ordered and filtered by the server.
 *
 * `total` counts every account matching the search and the filters rather than the rows of
 * this page, because that is what the pager draws itself from. Filtering or ordering in the
 * browser would apply to one page while `total` still counted everything - four pages of
 * five rows when the filter admitted one - and a list that contradicts its own pager is
 * worse than one that does not filter at all. So `total` is counted before the page is cut,
 * under the same predicate, in the same request.
 */
export interface AdminUserList {
  /** Accounts on this page, in the order asked for. */
  items: AdminUser[]
  /** How many accounts match, in total. */
  total: number
  /** The page these accounts are, one based. */
  page: number
  /** How many accounts one page holds. */
  pageSize: number
}
