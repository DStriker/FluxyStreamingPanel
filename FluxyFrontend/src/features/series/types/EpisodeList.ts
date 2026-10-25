import type { Episode } from './Episode'

/** One page of the episode table of one series. */
export interface EpisodeList {
  /** The episodes this page holds. */
  items: Episode[]
  /** How many episodes the same filter admits, all pages together. */
  total: number
  /** One-based page this answer is. */
  page: number
  /** How many episodes one page holds. */
  pageSize: number
}
