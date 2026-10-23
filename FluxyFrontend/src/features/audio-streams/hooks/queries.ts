import { useQuery } from '@tanstack/react-query'
import { getAudioStream, getAudioStreams } from '../api'
import type { AudioStreamSortField, AudioStreamSortOrder } from '../api'
import { detailId } from '../../../utils/detailId'
import type { AudioStreamStatus, AudioStreamType } from '../types'

/**
 * The audio-streams feature's reads: the paged streams table and the detail read the edit
 * form draws.
 *
 * One hook per endpoint with its key in this file beside it, every `queryFn` passing
 * React Query's `AbortSignal` through to the API.
 */

/** One page of the audio streams table, keyed by everything the server filters, sorts and pages by. */
export const useAudioStreams = (params: {
  page: number
  pageSize: number
  search?: string
  categoryId?: string | null
  status?: AudioStreamStatus | null
  type?: AudioStreamType | null
  sortBy: AudioStreamSortField
  sortOrder: AudioStreamSortOrder
}) =>
  useQuery({
    queryKey: ['audioStreams', params],
    queryFn: ({ signal }) => getAudioStreams({ ...params, signal }),
  })

/**
 * One stream in full, for the edit form. Not fetched on the add address, whose `enabled`
 * is false - a create has nothing to load, and asking would draw a spinner over a form
 * that is ready.
 */
export const useAudioStreamDetail = (id: string | undefined) =>
  useQuery({
    queryKey: ['audioStreams', 'detail', id],
    queryFn: ({ signal }) => getAudioStream(detailId(id), signal),
    enabled: id !== undefined,
  })
