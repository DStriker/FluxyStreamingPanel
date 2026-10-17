import type { VideoStream } from './VideoStream'

/**
 * `GET /admin/video-streams` - one page of the streams, ordered and filtered by the server.
 *
 * The same shape the accounts and the groups pages answer with, and for the same reason
 * every time: `total` is the size of the **filtered** list, counted before the page is cut,
 * under the same predicate. A search or a category filter applied in the browser would
 * narrow the page on screen while the pager still counted everything - four pages of five
 * streams when the filter admitted one - and a list that contradicts its own pager is worse
 * than one that does not filter at all.
 *
 * It is also why search, filters, order and paging all travel as query parameters: they
 * have to be applied *before* the count, and only the server can do that.
 */
export interface VideoStreamList {
  /** Streams on this page, in the order asked for. */
  items: VideoStream[]
  /** How many streams match, in total. */
  total: number
  /** The page these streams are, one based. */
  page: number
  /** How many streams one page holds. */
  pageSize: number
}
