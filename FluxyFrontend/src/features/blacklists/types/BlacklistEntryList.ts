import type { BlacklistEntry } from './BlacklistEntry'

/**
 * `GET /admin/blacklist-entries` - one page of one list, ordered and filtered by the server.
 * `total` is the size of the filtered list, counted before the page is cut.
 */
export interface BlacklistEntryList {
  /** Entries on this page, in the order asked for. */
  items: BlacklistEntry[]
  /** How many entries match, in total. */
  total: number
  /** The page these entries are, one based. */
  page: number
  /** How many entries one page holds. */
  pageSize: number
}
