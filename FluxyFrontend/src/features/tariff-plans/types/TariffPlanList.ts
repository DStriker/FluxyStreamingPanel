import type { TariffPlan } from './TariffPlan'

/**
 * One page of the tariff plans table, with the count its pager is built from.
 *
 * `total` is the size of the filtered list rather than of the table, which is the whole reason
 * search and order are answered by the server: a filter applied in the browser would narrow
 * the page on screen while the pager still counted everything.
 */
export interface TariffPlanList {
  /** The plans this page holds, in the requested order. */
  items: TariffPlan[]
  /** How many plans the same filter admits, all pages together. */
  total: number
  /** One-based page this answer is. */
  page: number
  /** How many plans one page holds. */
  pageSize: number
}
