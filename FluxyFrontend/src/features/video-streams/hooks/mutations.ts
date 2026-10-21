import {
  bulkVideoStreams,
  createVideoStream,
  deleteVideoStream,
  restartVideoStream,
  startVideoStream,
  stopVideoStream,
  updateVideoStream,
} from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The video-streams feature's writes: the form's create/update pair, the four row actions of
 * the table, and the bulk run a selection makes.
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

/** Marks one stream as broadcasting again. */
export const useRestartVideoStream = () => useWrite(restartVideoStream)

export const useDeleteVideoStream = () => useWrite(deleteVideoStream)

/** Applies one operation to every stream the table has selected. */
export const useBulkVideoStreams = () => useWrite(bulkVideoStreams)
