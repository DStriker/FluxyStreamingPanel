import type { VideoStreamImportProblem } from './VideoStreamImportProblem'

/**
 * The answer to a playlist import: how many entries the file held, how many landed, and
 * what happened to the rest.
 *
 * Always a `200` when the file was readable, even when nothing landed - the request was
 * carried out and the answer is the counts. `imported + skipped + failed` is `total`, and
 * `problems` holds only the entries that did not become a stream.
 */
export interface VideoStreamImportResponse {
  /** Machine readable outcome of the import as a whole. */
  code: string
  /** English fallback sentence naming what was done. */
  message: string
  /** How many entries the file held, including the ones that could not be used. */
  total: number
  /** How many streams were written. */
  imported: number
  /** How many existing streams were merged with import settings. */
  updated: number
  /** How many entries were left alone because their name was already taken. */
  skipped: number
  /** How many entries could not become a stream. */
  failed: number
  /** The entries that were skipped or refused, in the order of the file. */
  problems: VideoStreamImportProblem[]
}
