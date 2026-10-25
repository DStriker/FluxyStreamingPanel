import type { SeriesStatus } from './SeriesStatus'

/**
 * One row of the admin series table: `GET /admin/series`.
 *
 * `seasonsCount` is the largest season number the series holds, not the number of distinct
 * seasons, and both counts are projections the server computes over the episode rows.
 */
export interface Series {
  /** Identifier of the series, as text. */
  id: string
  /** Display name, unique within its category. */
  name: string
  /** Whether the series is published. */
  status: SeriesStatus
  /** Identifier of the category, as text. */
  categoryId: string
  /** Display name of that category. */
  categoryName: string
  /** Portrait cover address, or null. */
  posterUrl: string | null
  /** The largest season number the series holds, or zero. */
  seasonsCount: number
  /** How many episode rows the series holds. */
  episodesCount: number
  /** Release date as `yyyy-MM-dd`, or null. */
  releaseDate: string | null
  /** Rating from 0 to 10, or null. */
  rating: number | null
}
