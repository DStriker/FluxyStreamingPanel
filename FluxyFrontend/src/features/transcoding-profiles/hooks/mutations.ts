import {
  createTranscodingProfile,
  deleteTranscodingProfile,
  updateTranscodingProfile,
} from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The transcoding profiles feature's writes: the create and the change the form sends, and the
 * delete the table's row action sends.
 *
 * Built on `useWrite`, so the antiforgery token is minted at call time and the call sites hand
 * over only the endpoint's own arguments.
 */

export const useCreateTranscodingProfile = () => useWrite(createTranscodingProfile)
export const useUpdateTranscodingProfile = () => useWrite(updateTranscodingProfile)
export const useDeleteTranscodingProfile = () => useWrite(deleteTranscodingProfile)
