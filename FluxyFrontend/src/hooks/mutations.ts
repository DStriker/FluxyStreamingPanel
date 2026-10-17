import { useMutation } from '@tanstack/react-query'
import {
  bulkUserGroups,
  bulkUsers,
  changeEmail,
  changeLoginGuard,
  changePassword,
  changeUsername,
  confirmPasswordReset,
  confirmProfileChange,
  confirmRegistration,
  confirmUserRegistration,
  createContentCategory,
  createUser,
  createUserGroup,
  createVideoStream,
  deleteContentCategory,
  deleteUser,
  deleteUserGroup,
  deleteVideoStream,
  reorderContentCategories,
  requestPasswordReset,
  revokeActiveSession,
  revokeAdminSession,
  revokeOtherActiveSessions,
  setUserBlocked,
  setUserUnblocked,
  signOut,
  startVideoStream,
  stopVideoStream,
  submitAuth,
  submitRegistration,
  updateContentCategory,
  updateTimezone,
  updateUser,
  updateUserGroup,
  updateVideoStream,
} from '../lib/api'
import { getCsrfToken } from '../lib/csrf'

/**
 * The write side of the API, as the hooks the pages call - the counterpart of
 * `queries.ts`, which is the read side.
 *
 * One hook per write endpoint, named after it. The contract of every hook built on
 * `useWrite` below is the same: `mutateAsync` takes the endpoint's own arguments
 * **without the antiforgery token**, which is minted at call time inside the hook.
 * `src/lib/api` and `csrf.ts` both document why a token is minted immediately before the
 * request and never remembered - the antiforgery service binds it to the identity that
 * was current when it was minted, and one carried across a session change comes back
 * `csrf_invalid` - and this layer is where that rule is now structural: a caller cannot
 * forget the token, mint one too early, or keep one across a refresh.
 *
 * The captcha token is the exception and stays with the caller: it is minted for an
 * action the endpoint fixes and the *form* knows (`ProfileChangeCaptchaAction`,
 * `PasswordResetCaptchaAction`, ...), so it travels in the arguments like any value.
 *
 * Nothing here reports, refetches or navigates. That is the caller's, and the shapes the
 * screens share (the row action's toast and re-read, the bulk run's summary, a form's
 * field errors) live where they already did. What this file fixes is which request a
 * page makes and what it has to hand over to make it.
 */

/**
 * One write endpoint whose only token is the antiforgery one: mint it, call, and let the
 * rejection reach the caller as the `ApiError` it is.
 *
 * The cast exists because the compiler cannot follow the generic: `args` is the
 * endpoint's own argument type with the token removed (`Omit<Args, 'csrfToken'>`) and
 * the token is spread back in, which is exactly the shape the endpoint declares - but
 * for a generic `Args` that may carry properties of its own, TypeScript does not prove
 * that an object built this way *is* `Args`. One cast, here, rather than at each call
 * site.
 */
const useWrite = <Args extends { csrfToken: string | null }, Result>(
  call: (args: Args) => Promise<Result>,
) =>
  useMutation({
    mutationFn: async (args: Omit<Args, 'csrfToken'>): Promise<Result> =>
      call({ ...args, csrfToken: await getCsrfToken() } as Args),
  })

// The guest flows, in the order a visitor walks them.

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
 * `src/lib/api/auth.ts` - so this hook takes no arguments at all.
 */
export const useSignOut = () => useMutation({ mutationFn: () => signOut() })

// The sessions of one account: the administrator's history row action, and the two
// revokes of the account's own active-sessions page.

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

// The profile page.

/** Starts a login-name change; `profile_change_submitted` means a code is on its way. */
export const useChangeUsername = () => useWrite(changeUsername)

/** Starts an address change; the code goes to the *new* address. */
export const useChangeEmail = () => useWrite(changeEmail)

/** Starts a password change; confirming it ends every other session of the account. */
export const useChangePassword = () => useWrite(changePassword)

/** Applies a pending change with the code that was mailed out. */
export const useConfirmProfileChange = () => useWrite(confirmProfileChange)

/** Starts a login-guard change: the two switches and the three allow lists. */
export const useChangeLoginGuard = () => useWrite(changeLoginGuard)

/** Saves the display time zone - the one profile write with no captcha of its own. */
export const useUpdateTimezone = () => useWrite(updateTimezone)

// The accounts table and its form.

export const useCreateUser = () => useWrite(createUser)
export const useUpdateUser = () => useWrite(updateUser)

/** Confirms an account still waiting for its email code. */
export const useConfirmUserRegistration = () => useWrite(confirmUserRegistration)

export const useSetUserBlocked = () => useWrite(setUserBlocked)
export const useSetUserUnblocked = () => useWrite(setUserUnblocked)
export const useDeleteUser = () => useWrite(deleteUser)

/** Runs one operation over a set of accounts; the answer carries a verdict per id. */
export const useBulkUsers = () => useWrite(bulkUsers)

// The groups table and its form.

export const useCreateUserGroup = () => useWrite(createUserGroup)
export const useUpdateUserGroup = () => useWrite(updateUserGroup)
export const useDeleteUserGroup = () => useWrite(deleteUserGroup)
export const useBulkUserGroups = () => useWrite(bulkUserGroups)

// The content categories table, one per content section.

export const useCreateContentCategory = () => useWrite(createContentCategory)
export const useUpdateContentCategory = () => useWrite(updateContentCategory)
export const useDeleteContentCategory = () => useWrite(deleteContentCategory)

/** Sends the whole arrangement on screen as the new order, in one request. */
export const useReorderContentCategories = () => useWrite(reorderContentCategories)

// The video streams table and its form.

export const useCreateVideoStream = () => useWrite(createVideoStream)
export const useUpdateVideoStream = () => useWrite(updateVideoStream)

/** Marks one stream as broadcasting. Starting one that already runs is a success. */
export const useStartVideoStream = () => useWrite(startVideoStream)

/** Marks one stream as no longer broadcasting. */
export const useStopVideoStream = () => useWrite(stopVideoStream)

export const useDeleteVideoStream = () => useWrite(deleteVideoStream)
