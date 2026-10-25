import type { Series } from './Series'

/** One page of the series table: `GET /admin/series`. */
export interface SeriesList {
  /** The series this page holds. */
  items: Series[]
  /** How many series the same filter admits, all pages together. */
  total: number
  /** One-based page this answer is. */
  page: number
  /** How many series one page holds. */
  pageSize: number
}
