/**
 * One row of the bouquets table: the package, its position, and how much it holds.
 *
 * The counts arrive on every row because they are what the row's delete button tells the
 * truth about: a bouquet that still holds content cannot be deleted, and a button disabled
 * for no visible reason is a button that lies. The four kinds are counted apart because a
 * bouquet holds each of them in its own order.
 *
 * `sortOrder` is the position the row holds, not the position it is shown at: the rows
 * arrive already ordered by it, so a client that draws them in order never needs to read
 * it.
 */
export interface Bouquet {
  /** Identifier of the bouquet, as text. */
  id: string
  /** Display name, unique for the installation without regard to case. */
  name: string
  /** Position among the bouquets, lowest first. */
  sortOrder: number
  /** How many video streams the bouquet holds. */
  videoStreamCount: number
  /** How many audio streams the bouquet holds. */
  audioStreamCount: number
  /** How many on-demand videos the bouquet holds. */
  videoOnDemandCount: number
  /** How many series the bouquet holds. */
  seriesCount: number
}

/**
 * How much a bouquet holds in total, across the four kinds.
 *
 * The one number the delete button reads: anything above zero is a bouquet that must be
 * emptied first, and the four counts beside it say what to empty.
 */
export const bouquetItemCount = (bouquet: Bouquet): number =>
  bouquet.videoStreamCount +
  bouquet.audioStreamCount +
  bouquet.videoOnDemandCount +
  bouquet.seriesCount
