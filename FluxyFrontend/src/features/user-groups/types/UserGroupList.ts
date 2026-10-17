import type { UserGroup } from './UserGroup'

/**
 * `GET /admin/user-groups` - one page of the groups, ordered and filtered by the server.
 *
 * The same shape the accounts page answers with, for the same reason: `total` is the size of
 * the **filtered** list, counted before the page is cut, under the same predicate. A filter
 * applied in the browser would narrow one page while the pager still counted everything -
 * four pages of five groups when the filter admitted one - and a list that contradicts its
 * own pager is worse than one that does not filter at all.
 *
 * A picker that wants every group asks for one page of 100 (`UserGroupListLimits.MaxPageSize`
 * on the backend) rather than this resource growing a second unpaged route: the count a
 * picker would ignore is the same number the table needs, and one route is one thing to keep
 * correct.
 */
export interface UserGroupList {
  /** Groups on this page, in the order asked for. */
  items: UserGroup[]
  /** How many groups match, in total. */
  total: number
  /** The page these groups are, one based. */
  page: number
  /** How many groups one page holds. */
  pageSize: number
}
