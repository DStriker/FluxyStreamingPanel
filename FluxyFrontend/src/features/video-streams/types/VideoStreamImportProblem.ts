import type { FieldErrors } from '../../../types'

/**
 * One playlist entry that did not become a stream.
 *
 * A skipped entry was a valid stream whose name another row already held; a failed entry
 * could not become a stream at all. Both carry the one-based line of the uploaded file so
 * the operator can open it at the right place, and `name` is null only when the line named
 * none.
 */
export interface VideoStreamImportProblem {
  /** One-based line of the entry in the uploaded file. */
  line: number
  /** Name the entry would have carried, or null when the line named none. */
  name: string | null
  /** Whether the entry was left alone rather than refused. */
  skipped: boolean
  /** Machine readable name of what happened. */
  code: string
  /** English fallback sentence for the entry. */
  message: string
  /** Rejected fields, keyed by the camelCase name of the form field. */
  errors: FieldErrors
}
