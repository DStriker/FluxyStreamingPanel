import {
  bulkUsers,
  confirmUserRegistration,
  createUser,
  deleteUser,
  setUserBlocked,
  setUserUnblocked,
  updateUser,
} from '../../../lib/api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The users feature's writes: the create/update form, the row actions and the bulk run.
 *
 * Built on `useWrite` (`src/hooks/useWrite.ts`), so `mutateAsync` takes the endpoint's
 * own arguments without the antiforgery token. Nothing here reports, refetches or
 * navigates - that stays with the page.
 */

export const useCreateUser = () => useWrite(createUser)
export const useUpdateUser = () => useWrite(updateUser)

/** Confirms an account still waiting for its email code. */
export const useConfirmUserRegistration = () => useWrite(confirmUserRegistration)

export const useSetUserBlocked = () => useWrite(setUserBlocked)
export const useSetUserUnblocked = () => useWrite(setUserUnblocked)
export const useDeleteUser = () => useWrite(deleteUser)

/** Runs one operation over a set of accounts; the answer carries a verdict per id. */
export const useBulkUsers = () => useWrite(bulkUsers)
