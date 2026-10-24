import type { VideoOnDemandStatus } from './VideoOnDemandStatus'
import type { VideoOnDemandType } from './VideoOnDemandType'

/**
 * One row of the on-demand table: what a line of it draws.
 *
 * `GET /admin/video-on-demand` answers with these. The source is deliberately absent: the
 * play button goes through the endpoint that owns the delivery rule, so the table does not
 * need the address and a page of twenty rows does not carry twenty long paths.
 */
export interface VideoOnDemand {
  /** Identifier of the video, as text. The table shows it shortened. */
  id: string
  /** Display name, unique within its category and compared case sensitively. */
  name: string
  /** Whether the video is published, as the name of the enum member. */
  status: VideoOnDemandStatus
  /** How the video is delivered, as the name of the enum member. */
  type: VideoOnDemandType
  /** Identifier of the category, as text. */
  categoryId: string
  /** Display name of that category, which is what the column draws. */
  categoryName: string
  /** Portrait cover address, or null. */
  posterUrl: string | null
  /** Release date as `yyyy-MM-dd`, or null. */
  releaseDate: string | null
  /** Rating from 0 to 10, or null. */
  rating: number | null
}
