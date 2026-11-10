import { clearStreamProblems } from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The stream problem journal's one write: the whole-table clear.
 *
 * Built on `useWrite`, so the antiforgery token is minted at call time and the call site hands
 * over no arguments at all - the endpoint takes none. `editStreams` gates it, which is the
 * grant that also gates changing a stream: the wipe destroys the streams' own record.
 */
export const useClearStreamProblems = () => useWrite(clearStreamProblems)
