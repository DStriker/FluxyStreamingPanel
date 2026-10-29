import type { Subscription } from './Subscription'

/**
 * One page of the subscriptions table, with the count its pager is built from.
 *
 * `total` is the size of the filtered list rather than of the table, which is the whole reason
 * the search, the three filters and the two orders are answered by the server.
 */
export interface SubscriptionList {
  /** The subscriptions this page holds, in the requested order. */
  items: Subscription[]
  /** How many subscriptions the same filter admits, all pages together. */
  total: number
  /** One-based page this answer is. */
  page: number
  /** How many subscriptions one page holds. */
  pageSize: number
}
