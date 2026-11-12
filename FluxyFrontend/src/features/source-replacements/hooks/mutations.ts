import { replaceSources } from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The source replacement's one write.
 *
 * Built on `useWrite`, so the antiforgery token is minted at call time and the call site hands
 * over only the two strings. The four edit grants the endpoint demands are the page's concern:
 * this hook is called only after the profile has said the operator holds all of them.
 */
export const useReplaceSources = () => useWrite(replaceSources)
