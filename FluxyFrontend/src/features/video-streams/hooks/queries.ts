import { useQuery } from '@tanstack/react-query'
import { getVideoStream, getVideoStreams } from '../api'
import type { VideoStreamSortField, VideoStreamSortOrder } from '../api'
import { detailId } from '../../../lib/detailId'
import type { VideoStreamStatus, VideoStreamType } from '../types'

/**
 * The video-streams feature's reads: the paged streams table and the detail read the edit
 * form draws.
 *
 * One hook per endpoint with its key in this file beside it, every `queryFn` passing
 * React Query's `AbortSignal` through to the API.
 */

/** One page of the streams table, keyed by everything the server filters, sorts and pages by. */
export const useVideoStreams = (params: {
  page: number
  pageSize: number
  search?: string
  categoryId?: string | null
  status?: VideoStreamStatus | null
  type?: VideoStreamType | null
  sortBy: VideoStreamSortField
  sortOrder: VideoStreamSortOrder
}) =>
  useQuery({
    queryKey: ['videoStreams', params],
    queryFn: ({ signal }) => getVideoStreams({ ...params, signal }),
  })

/**
 * One stream in full, for the edit form. Not fetched on the add address, whose `enabled`
 * is false - a create has nothing to load, and asking would draw a spinner over a form
 * that is ready.
 */
export const useVideoStreamDetail = (id: string | undefined) =>
  useQuery({
    queryKey: ['videoStreams', 'detail', id],
    queryFn: ({ signal }) => getVideoStream(detailId(id), signal),
    enabled: id !== undefined,
  })
