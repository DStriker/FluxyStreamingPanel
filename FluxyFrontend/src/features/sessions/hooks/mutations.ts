import { useMutation } from '@tanstack/react-query'
import {
  revokeActiveSession,
  revokeAdminSession,
  revokeOtherActiveSessions,
} from '../api'
import { getCsrfToken } from '../../../utils/csrf'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The sessions feature's writes: the administrator's history row action, and the two
 * revokes of the account's own active-sessions page.
 *
 * `useRevokeOtherActiveSessions` is the one write here that does not go through
 * `useWrite`: the endpoint has no argument of its own - the caller's session comes from
 * the token - and `useWrite` would make the caller pass an empty object to say nothing.
 */

/** Ends one session from the administrator's visit history. */
export const useRevokeAdminSession = () => useWrite(revokeAdminSession)

/** Ends one session of the caller's own account. */
export const useRevokeActiveSession = () => useWrite(revokeActiveSession)

/**
 * Ends every session of the caller's own account except the one this browser holds.
 *
 * Spelled without `useWrite` because the endpoint has no argument of its own - the
 * caller's session comes from the token - and `useWrite` would make the caller pass an
 * empty object to say nothing.
 */
export const useRevokeOtherActiveSessions = () =>
  useMutation({
    mutationFn: async () => revokeOtherActiveSessions({ csrfToken: await getCsrfToken() }),
  })
