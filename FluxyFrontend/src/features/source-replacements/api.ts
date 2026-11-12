import { apiFetch } from '../../utils/http'
import type { SourceReplacementResult } from './types'

/**
 * The mass source replacement: one request that rewrites every playback address.
 *
 * One endpoint and nothing else. The run has no resource to read back and no member to
 * address - it reads every source column of four tables and writes a replacement over each
 * occurrence of the search text - so the page has no `queries.ts` and no list to show. What
 * comes back is the report: one count per domain and the entities the run declined to touch.
 */

/** What the form sends: the text to find and the text written over it. */
export interface SourceReplacementPayload {
  find: string
  /**
   * The replacement. An empty string is meaningful - it deletes every occurrence - so the
   * field is required by the type even though the server tolerates its absence.
   */
  replace: string
}

/**
 * Applies the replacement: `POST /admin/source-replacements`.
 *
 * The four edit grants of the four content domains are required together, which is the
 * server's fence rather than something this call can express; the page asks the profile for
 * them before it offers the button. The antiforgery token arrives from `useWrite`, never from
 * the call site.
 */
export const replaceSources = ({
  payload,
  csrfToken,
}: {
  payload: SourceReplacementPayload
  csrfToken: string | null
}): Promise<SourceReplacementResult> =>
  apiFetch('/admin/source-replacements', {
    method: 'POST',
    body: payload,
    csrfToken,
  })
