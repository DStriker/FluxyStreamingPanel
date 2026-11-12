import type { SourceReplacementDomain } from './SourceReplacementDomainResult'

/**
 * One entity the run declined to touch.
 *
 * The refusal is all-or-nothing per entity: `code` says why (`source_replacement_blank` or
 * `source_replacement_too_long`) and the name is what the dialog shows, because an operator
 * finds a row by its name rather than by its identifier.
 */
export interface SourceReplacementRefusal {
  domain: SourceReplacementDomain
  id: string
  name: string
  code: string
}
