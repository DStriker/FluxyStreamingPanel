import { useQuery } from '@tanstack/react-query'
import {
  getVideoOnDemand,
  getVideoOnDemandDetail,
  getVideoOnDemandTranscodeFormats,
} from '../api'
import type { VideoOnDemandSortField, VideoOnDemandSortOrder } from '../api'
import { detailId } from '../../../utils/detailId'
import type { VideoOnDemandStatus, VideoOnDemandType } from '../types'

/**
 * The on-demand feature's reads: the paged catalogue, the detail the edit form draws, and
 * the one availability answer the table asks before drawing the transcode action.
 *
 * One hook per endpoint with its key in this file beside it, every `queryFn` passing React
 * Query's `AbortSignal` through to the API.
 */

/** One page of the table, keyed by everything the server filters, sorts and pages by. */
export const useVideoOnDemand = (params: {
  page: number
  pageSize: number
  search?: string
  categoryId?: string | null
  status?: VideoOnDemandStatus | null
  type?: VideoOnDemandType | null
  sortBy: VideoOnDemandSortField
  sortOrder: VideoOnDemandSortOrder
}) =>
  useQuery({
    queryKey: ['videoOnDemand', params],
    queryFn: ({ signal }) => getVideoOnDemand({ ...params, signal }),
  })

/**
 * One video in full. Not fetched on the add address, whose `id` is undefined - a create has
 * nothing to load, and asking would draw a spinner over a form that is ready.
 */
export const useVideoOnDemandDetail = (id: string | undefined) =>
  useQuery({
    queryKey: ['videoOnDemand', 'detail', id],
    queryFn: ({ signal }) => getVideoOnDemandDetail(detailId(id), signal),
    enabled: id !== undefined,
  })

/**
 * Whether the installation can transcode. The key is not parameterised because the answer is
 * a fact about the server, and the table reads one cached answer for every row.
 */
export const useVideoOnDemandTranscodeFormats = () =>
  useQuery({
    queryKey: ['videoOnDemand', 'transcodeFormats'],
    queryFn: ({ signal }) => getVideoOnDemandTranscodeFormats(signal),
  })
