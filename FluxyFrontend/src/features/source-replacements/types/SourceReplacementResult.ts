import type { SourceReplacementDomainResult } from './SourceReplacementDomainResult'
import type { SourceReplacementRefusal } from './SourceReplacementRefusal'

/**
 * The answer of one replacement run.
 *
 * `code` is `source_replacement_completed` on every valid run - including one that matched
 * nothing, which is a success with `updated: 0`. `updated` and `skipped` are the sums of the
 * domain counts, sent by the server so the page does not have to add them.
 */
export interface SourceReplacementResult {
  code: string
  message: string
  updated: number
  skipped: number
  domains: SourceReplacementDomainResult[]
  refusals: SourceReplacementRefusal[]
}
