import { clearApiRequestFailures } from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The API request failure log's one write: the whole-table clear.
 *
 * Built on `useWrite`, so the antiforgery token is minted at call time and the call site hands
 * over no arguments at all - the endpoint takes none. `viewApiRequestFailures` gates it, which
 * is the same grant that gates the read: the wipe destroys the same evidence.
 */
export const useClearApiRequestFailures = () => useWrite(clearApiRequestFailures)
