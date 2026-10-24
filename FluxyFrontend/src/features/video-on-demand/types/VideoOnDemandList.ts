import type { VideoOnDemand } from './VideoOnDemand'

/**
 * `GET /admin/video-on-demand` - one page of the catalogue, ordered and filtered by the
 * server. `total` is the size of the filtered list, counted before the page is cut.
 */
export interface VideoOnDemandList {
  /** Videos on this page, in the order asked for. */
  items: VideoOnDemand[]
  /** How many videos match, in total. */
  total: number
  /** The page these videos are, one based. */
  page: number
  /** How many videos one page holds. */
  pageSize: number
}
