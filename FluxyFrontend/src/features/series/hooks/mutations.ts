import {
  bulkEpisodes,
  bulkSeries,
  createEpisode,
  createSeason,
  createSeries,
  deleteEpisode,
  deleteSeries,
  requestEpisodeTranscode,
  setEpisodeStatus,
  setSeriesStatus,
  updateEpisode,
  updateSeries,
} from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The series feature's writes: the two form pairs, the row actions, the two bulk runs, the
 * season creation and the transcoding request.
 *
 * Built on `useWrite`, so the antiforgery token is minted at call time and the call sites hand
 * over only the endpoint's own arguments.
 */

export const useCreateSeries = () => useWrite(createSeries)
export const useUpdateSeries = () => useWrite(updateSeries)
export const useSetSeriesStatus = () => useWrite(setSeriesStatus)
export const useDeleteSeries = () => useWrite(deleteSeries)
export const useBulkSeries = () => useWrite(bulkSeries)

export const useCreateEpisode = () => useWrite(createEpisode)
export const useUpdateEpisode = () => useWrite(updateEpisode)
export const useSetEpisodeStatus = () => useWrite(setEpisodeStatus)
export const useDeleteEpisode = () => useWrite(deleteEpisode)
export const useBulkEpisodes = () => useWrite(bulkEpisodes)

export const useCreateSeason = () => useWrite(createSeason)

/** Asks for one episode to be converted; refused while no encoder is configured. */
export const useRequestEpisodeTranscode = () => useWrite(requestEpisodeTranscode)
