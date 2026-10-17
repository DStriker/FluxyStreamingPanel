import { useMutation } from '@tanstack/react-query'
import {
  confirmPasswordReset,
  confirmRegistration,
  requestPasswordReset,
  signOut,
  submitAuth,
  submitRegistration,
} from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The auth feature's writes: the guest flows in the order a visitor walks them, and the
 * one write the signed-in shell performs.
 *
 * All but the last are built on `useWrite` (`src/hooks/useWrite.ts`), so the antiforgery
 * token is minted at call time and the call sites hand over only the values and the
 * captcha token. `useSignOut` is the special case its endpoint forces: `signOut` mints
 * its own token because it must - see the note above it in `src/features/auth/api.ts` - so
 * this hook takes no arguments at all.
 */

/** One of the three sign-in forms - `client-login`, `reseller-login`, `admin-login`. */
export const useSubmitAuth = () => useWrite(submitAuth)

/** Step one of registration: the account details. */
export const useSubmitRegistration = () => useWrite(submitRegistration)

/** Step two of registration: the code that was mailed out. */
export const useConfirmRegistration = () => useWrite(confirmRegistration)

/** Step one of the public password reset. */
export const useRequestPasswordReset = () => useWrite(requestPasswordReset)

/** Step two of the public password reset. */
export const useConfirmPasswordReset = () => useWrite(confirmPasswordReset)

/**
 * Ends the session. `signOut` mints its own token on purpose - see the note above it in
 * `src/features/auth/api.ts` - so this hook takes no arguments at all.
 */
export const useSignOut = () => useMutation({ mutationFn: () => signOut() })
