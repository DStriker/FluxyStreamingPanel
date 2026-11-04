import { clearConnectionLogs } from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The connection log's one write: the whole-table clear.
 *
 * Built on `useWrite`, so the antiforgery token is minted at call time and the call site hands
 * over no arguments at all - the endpoint takes none.
 */
export const useClearConnectionLogs = () => useWrite(clearConnectionLogs)
