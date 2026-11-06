import { clearCreditLogs } from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The credit log's one write: the whole-table clear.
 *
 * Built on `useWrite`, so the antiforgery token is minted at call time and the call site hands
 * over no arguments at all - the endpoint takes none.
 */
export const useClearCreditLogs = () => useWrite(clearCreditLogs)
