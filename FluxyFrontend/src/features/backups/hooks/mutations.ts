import { useWrite } from '../../../hooks/useWrite'
import { createBackup, deleteBackup, restoreBackup } from '../api'

/**
 * The backups feature's three writes, one hook per endpoint.
 *
 * Built on `useWrite`, so the antiforgery token is minted at call time and the call sites hand
 * over only the endpoint's own arguments - for the restore and the delete, the file name.
 *
 * Nothing here reports, refetches or navigates: those are the page's decisions, and a hook that
 * took them would be a page in disguise.
 */
export const useCreateBackup = () => useWrite(createBackup)
export const useRestoreBackup = () => useWrite(restoreBackup)
export const useDeleteBackup = () => useWrite(deleteBackup)
