import { useQuery } from '@tanstack/react-query'
import {
  ALL_EPG_SOURCES_PAGE_SIZE,
  getEpgSource,
  getEpgSourceChannels,
  getEpgSources,
} from '../api'
import type { EpgSourceChannelSortField, EpgSourceSortField, EpgSourceSortOrder } from '../api'
import { detailId } from '../../../utils/detailId'

/**
 * The EPG feature's reads: the paged source table, the whole-list picker the stream form
 * fills its guide-source field from, the detail the edit form draws, and the channels that
 * use one source.
 *
 * One hook per endpoint with its key in this file beside it, every `queryFn` passing React
 * Query's `AbortSignal` through to the API.
 */

/** One page of the table, keyed by everything the server searches, sorts and pages by. */
export const useEpgSources = (params: {
  page: number
  pageSize: number
  search?: string
  sortBy: EpgSourceSortField
  sortOrder: EpgSourceSortOrder
}) =>
  useQuery({
    queryKey: ['epgSources', params],
    queryFn: ({ signal }) => getEpgSources({ ...params, signal }),
  })

/**
 * The whole source list for a picker, for the reason `useAllUserGroups` gives in the groups
 * feature: the stream form needs every source to choose from, and a source list is short.
 */
export const useAllEpgSources = () =>
  useQuery({
    queryKey: ['epgSources', 'picker'],
    queryFn: ({ signal }) =>
      getEpgSources({
        page: 1,
        pageSize: ALL_EPG_SOURCES_PAGE_SIZE,
        sortBy: 'name',
        sortOrder: 'asc',
        signal,
      }),
  })

/** One source in full, for the edit form and the channels page's heading. */
export const useEpgSourceDetail = (id: string | undefined) =>
  useQuery({
    queryKey: ['epgSources', 'detail', id],
    queryFn: ({ signal }) => getEpgSource(detailId(id), signal),
    enabled: id !== undefined,
  })

/** The streams that read their guide from one source. */
export const useEpgSourceChannels = (
  id: string | undefined,
  params: {
    page: number
    pageSize: number
    search?: string
    sortBy: EpgSourceChannelSortField
    sortOrder: EpgSourceSortOrder
  },
) =>
  useQuery({
    queryKey: ['epgSources', 'channels', id, params],
    queryFn: ({ signal }) => getEpgSourceChannels({ ...params, id: detailId(id), signal }),
    enabled: id !== undefined,
  })
