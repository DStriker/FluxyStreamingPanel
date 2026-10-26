import { createEpgSource, deleteEpgSource, updateEpgSource } from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The EPG feature's writes: the form's create/update pair and the delete.
 *
 * Built on `useWrite`, so the antiforgery token is minted at call time and the call sites
 * hand over only the endpoint's own arguments.
 */

export const useCreateEpgSource = () => useWrite(createEpgSource)
export const useUpdateEpgSource = () => useWrite(updateEpgSource)
export const useDeleteEpgSource = () => useWrite(deleteEpgSource)
