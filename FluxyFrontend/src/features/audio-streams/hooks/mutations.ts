import {
  bulkAudioStreams,
  createAudioStream,
  deleteAudioStream,
  importAudioStreams,
  restartAudioStream,
  startAudioStream,
  stopAudioStream,
  updateAudioStream,
} from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The audio-streams feature's writes: the form's create/update pair, the four row actions of
 * the table, and the bulk run a selection makes.
 *
 * Built on `useWrite` (`src/hooks/useWrite.ts`), so the antiforgery token is minted at
 * call time and the call sites hand over only the endpoint's own arguments.
 */

export const useCreateAudioStream = () => useWrite(createAudioStream)
export const useUpdateAudioStream = () => useWrite(updateAudioStream)

/** Marks one stream as broadcasting. Starting one that already runs is a success. */
export const useStartAudioStream = () => useWrite(startAudioStream)

/** Marks one stream as no longer broadcasting. */
export const useStopAudioStream = () => useWrite(stopAudioStream)

/** Marks one stream as broadcasting again. */
export const useRestartAudioStream = () => useWrite(restartAudioStream)

export const useDeleteAudioStream = () => useWrite(deleteAudioStream)

/**
 * Creates the streams a playlist can supply.
 *
 * The one write of this feature whose answer is not a message: it carries the counts and the
 * per-line problems, so the page draws what the import did rather than a toast that would
 * have to reduce four numbers to one sentence.
 */
export const useImportAudioStreams = () => useWrite(importAudioStreams)

/** Applies one operation to every audio stream the table has selected. */
export const useBulkAudioStreams = () => useWrite(bulkAudioStreams)
