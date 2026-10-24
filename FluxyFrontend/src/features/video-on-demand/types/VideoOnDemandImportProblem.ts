/**
 * One playlist line that did not become a video.
 *
 * `skipped` tells the two answers apart without reading the code: a skipped entry was a
 * valid video whose name another row already held, while a failed entry could not become a
 * video at all.
 */
export interface VideoOnDemandImportProblem {
  /** One-based line of the entry in the uploaded file. */
  line: number
  /** Name the entry would have carried, or null. */
  name: string | null
  /** Whether the entry was left alone rather than refused. */
  skipped: boolean
  /** Machine readable name of what happened. */
  code: string
  /** English fallback sentence for the entry. */
  message: string
  /** Rejected fields, keyed by the camelCase name of the form field. */
  errors: Record<string, string[]> | null
}
