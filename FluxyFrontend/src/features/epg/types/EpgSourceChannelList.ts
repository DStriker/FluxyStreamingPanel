import type { EpgSourceChannel } from './EpgSourceChannel'

/**
 * `GET /admin/epg-sources/{id}/channels` - one page of the streams that read their guide
 * from a source, counted before the page is cut.
 */
export interface EpgSourceChannelList {
  /** Channels on this page, in the order asked for. */
  items: EpgSourceChannel[]
  /** How many channels match, in total. */
  total: number
  /** The page these channels are, one based. */
  page: number
  /** How many channels one page holds. */
  pageSize: number
}
