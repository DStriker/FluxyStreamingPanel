import { clearUserLogs } from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The user audit log's one write: the whole-table clear.
 *
 * Built on `useWrite`, so the antiforgery token is minted at call time and the call site
 * hands over no arguments at all - the endpoint takes none. `viewUserLogs` gates it, which
 * is the same grant that gates the read.
 */
export const useClearUserLogs = () => useWrite(clearUserLogs)
