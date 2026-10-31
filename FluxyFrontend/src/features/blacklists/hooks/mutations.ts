import {
  clearBlacklistEntries,
  createBlacklistEntry,
  deleteBlacklistEntry,
  updateBlacklistEntry,
} from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The blacklists feature's writes: the dialog's create/update pair, the row delete and the
 * clear-all command.
 *
 * Built on `useWrite`, so the antiforgery token is minted at call time and the call sites hand
 * over only the endpoint's own arguments.
 */
export const useCreateBlacklistEntry = () => useWrite(createBlacklistEntry)
export const useUpdateBlacklistEntry = () => useWrite(updateBlacklistEntry)
export const useDeleteBlacklistEntry = () => useWrite(deleteBlacklistEntry)
export const useClearBlacklistEntries = () => useWrite(clearBlacklistEntries)
