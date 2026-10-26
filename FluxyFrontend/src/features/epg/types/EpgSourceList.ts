import type { EpgSource } from './EpgSource'

/**
 * `GET /admin/epg-sources` - one page of the table, ordered and filtered by the server.
 * `total` is the size of the filtered list, counted before the page is cut.
 */
export interface EpgSourceList {
  /** Sources on this page, in the order asked for. */
  items: EpgSource[]
  /** How many sources match, in total. */
  total: number
  /** The page these sources are, one based. */
  page: number
  /** How many sources one page holds. */
  pageSize: number
}
