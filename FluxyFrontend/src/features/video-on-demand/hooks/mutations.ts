import {
  bulkVideoOnDemand,
  createVideoOnDemand,
  deleteVideoOnDemand,
  importVideoOnDemand,
  requestVideoOnDemandTranscode,
  setVideoOnDemandStatus,
  updateVideoOnDemand,
} from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The on-demand feature's writes: the form's create/update pair, the delete, the bulk run,
 * the playlist import and the transcode request.
 *
 * Built on `useWrite`, so the antiforgery token is minted at call time and the call sites
 * hand over only the endpoint's own arguments.
 */

export const useCreateVideoOnDemand = () => useWrite(createVideoOnDemand)
export const useUpdateVideoOnDemand = () => useWrite(updateVideoOnDemand)

/** Publishes or hides one video from the table's row action. */
export const useSetVideoOnDemandStatus = () => useWrite(setVideoOnDemandStatus)

export const useDeleteVideoOnDemand = () => useWrite(deleteVideoOnDemand)

/** Creates the videos a playlist can supply. */
export const useImportVideoOnDemand = () => useWrite(importVideoOnDemand)

/** Applies one operation to every video the table has selected. */
export const useBulkVideoOnDemand = () => useWrite(bulkVideoOnDemand)

/** Asks for one video to be converted; refused while no encoder is configured. */
export const useRequestVideoOnDemandTranscode = () =>
  useWrite(requestVideoOnDemandTranscode)
