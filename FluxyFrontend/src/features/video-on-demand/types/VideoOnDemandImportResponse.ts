import type { VideoOnDemandImportProblem } from './VideoOnDemandImportProblem'

/**
 * The answer to a playlist import: how many entries the file held, how many landed, and
 * what happened to the rest. Always a 200 when the file was readable, even when nothing
 * landed.
 */
export interface VideoOnDemandImportResponse {
  /** Machine readable outcome of the import as a whole. */
  code: string
  /** English fallback sentence naming what was done. */
  message: string
  /** How many entries the file held, including the ones that could not be used. */
  total: number
  /** How many videos were written. */
  imported: number
  /** How many entries were left alone because their name was already taken. */
  skipped: number
  /** How many entries could not become a video. */
  failed: number
  /** The entries that were skipped or refused, in the order of the file. */
  problems: VideoOnDemandImportProblem[]
}
