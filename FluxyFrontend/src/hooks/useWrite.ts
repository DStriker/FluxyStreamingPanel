import { useMutation } from '@tanstack/react-query'
import { getCsrfToken } from '../lib/csrf'

/**
 * One write endpoint whose only token is the antiforgery one: mint it, call, and let the
 * rejection reach the caller as the `ApiError` it is.
 *
 * Every write hook in the features is built on this one, so `mutateAsync` takes the
 * endpoint's own arguments **without the antiforgery token** wherever it is called from.
 * That rule is not a preference - the antiforgery service binds a token to the identity
 * that was current when it was minted, and one carried across a session change comes back
 * `csrf_invalid` - and this is where it is structural rather than remembered: a caller
 * cannot forget the token, mint one too early, or keep one across a refresh. The captcha
 * token is the exception and stays with the caller: it is minted for an action the
 * endpoint fixes and the form knows.
 *
 * The cast exists because the compiler cannot follow the generic: `args` is the
 * endpoint's own argument type with the token removed (`Omit<Args, 'csrfToken'>`) and
 * the token is spread back in, which is exactly the shape the endpoint declares - but
 * for a generic `Args` that may carry properties of its own, TypeScript does not prove
 * that an object built this way *is* `Args`. One cast, here, rather than at each call
 * site.
 */
export const useWrite = <Args extends { csrfToken: string | null }, Result>(
  call: (args: Args) => Promise<Result>,
) =>
  useMutation({
    mutationFn: async (args: Omit<Args, 'csrfToken'>): Promise<Result> =>
      call({ ...args, csrfToken: await getCsrfToken() } as Args),
  })
