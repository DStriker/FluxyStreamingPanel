import {
  createVideoStream,
  deleteVideoStream,
  startVideoStream,
  stopVideoStream,
  updateVideoStream,
} from '../../../lib/api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The video-streams feature's writes: the form's create/update pair and the three row
 * actions of the table.
 *
 * Built on `useWrite` (`src/hooks/useWrite.ts`), so the antiforgery token is minted at
 * call time and the call sites hand over only the endpoint's own arguments.
 */

export const useCreateVideoStream = () => useWrite(createVideoStream)
export const useUpdateVideoStream = () => useWrite(updateVideoStream)

/** Marks one stream as broadcasting. Starting one that already runs is a success. */
export const useStartVideoStream = () => useWrite(startVideoStream)

/** Marks one stream as no longer broadcasting. */
export const useStopVideoStream = () => useWrite(stopVideoStream)

export const useDeleteVideoStream = () => useWrite(deleteVideoStream)
