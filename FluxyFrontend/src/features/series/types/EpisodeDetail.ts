import type { Episode } from './Episode'

/** One episode in full: `GET /admin/series/{id}/episodes/{episodeId}`. */
export interface EpisodeDetail extends Episode {
  /** IMDb identifier, or null. */
  imdbId: string | null
  /** Synopsis, or null. */
  plot: string | null
}
