import type { EpisodeStatus } from './EpisodeStatus'
import type { EpisodeType } from './EpisodeType'

/**
 * One row of the episode table of one series: `GET /admin/series/{id}/episodes`.
 *
 * The source is present because this table is where an operator checks which file a row
 * points at; there is no delivery endpoint whose address could hide it.
 */
export interface Episode {
  /** Identifier of the episode, as text. */
  id: string
  /** Series the episode belongs to, as text. */
  seriesId: string
  /** Season the episode belongs to. */
  seasonNumber: number
  /** Number of the episode inside its season. */
  episodeNumber: number
  /** Display name, or null. */
  name: string | null
  /** Whether the episode is published. */
  status: EpisodeStatus
  /** How the episode is delivered. */
  type: EpisodeType
  /** URL or file path the episode is played from. */
  source: string
  /** Address of the episode image, or null. */
  imageUrl: string | null
  /** Release date as `yyyy-MM-dd`, or null. */
  releaseDate: string | null
  /** Rating from 0 to 10, or null. */
  rating: number | null
}
