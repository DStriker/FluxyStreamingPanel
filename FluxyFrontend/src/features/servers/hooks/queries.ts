import { useQuery } from '@tanstack/react-query'
import { getServer, getServers } from '../api'
import type { ServerSortField, ServerSortOrder } from '../api'
import { detailId } from '../../../utils/detailId'

/**
 * The servers feature's reads: one page of the table, and one server in full.
 *
 * One hook per endpoint with its key in this file beside it, every `queryFn` passing React
 * Query's `AbortSignal` through to the API.
 */

/** One page of the table, keyed by everything the server searches, sorts and pages by. */
export const useServers = (params: {
  page: number
  pageSize: number
  search?: string
  sortBy: ServerSortField
  sortOrder: ServerSortOrder
}) =>
  useQuery({
    queryKey: ['servers', params],
    queryFn: ({ signal }) => getServers({ ...params, signal }),
  })

/**
 * One server in full, for the edit form.
 *
 * Disabled while there is no identifier, which is what makes one component serve the add
 * address as well as the edit one. `detailId` states that invariant in code rather than in a
 * cast.
 */
export const useServer = (id: string | undefined) =>
  useQuery({
    queryKey: ['servers', 'detail', id],
    queryFn: ({ signal }) => getServer(detailId(id), signal),
    enabled: id !== undefined,
  })
