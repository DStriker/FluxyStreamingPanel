import type { Series } from './Series'

/**
 * One series in full: `GET /admin/series/{id}`.
 *
 * The metadata the table does not draw is here, together with the two counts the heading and
 * the form show read-only.
 */
export interface SeriesDetail extends Series {
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
}
