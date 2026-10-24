import type { VideoOnDemand } from './VideoOnDemand'

/**
 * One on-demand video in full, for the edit form.
 *
 * `GET /admin/video-on-demand/{id}` answers with this: the list row plus everything the
 * table deliberately leaves out. The two lists arrive in the order the operator entered
 * them, and a save sends them back in that same order.
 */
export interface VideoOnDemandDetail extends VideoOnDemand {
  /** URL or file path the video is played from. */
  source: string
  /** Subtitles address, or null. */
  subtitleUrl: string | null
  /** IMDb identifier, or null. */
  imdbId: string | null
  /** Wide background picture address, or null. */
  backdropUrl: string | null
  /** Synopsis, or null. */
  plot: string | null
  /** Cast, in billing order. */
  cast: string[]
  /** Director, or null. */
  director: string | null
  /** Genres, in the order entered. */
  genres: string[]
  /** Upper case ISO 3166-1 alpha-2 country code, or null. */
  countryCode: string | null
}
