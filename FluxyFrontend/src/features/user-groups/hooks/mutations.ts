import {
  bulkUserGroups,
  createUserGroup,
  deleteUserGroup,
  updateUserGroup,
} from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The user-groups feature's writes: the create/update form's two endpoints, the row
 * actions, and the bulk run.
 *
 * Built on `useWrite` (`src/hooks/useWrite.ts`), so the antiforgery token is minted at
 * call time and the call sites hand over only the endpoint's own arguments.
 */

export const useCreateUserGroup = () => useWrite(createUserGroup)
export const useUpdateUserGroup = () => useWrite(updateUserGroup)
export const useDeleteUserGroup = () => useWrite(deleteUserGroup)
export const useBulkUserGroups = () => useWrite(bulkUserGroups)
