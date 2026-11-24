import { useQuery } from '@tanstack/react-query'
import { getServer, getServerOptions, getServers } from '../api'
import type { ServerSortField, ServerSortOrder } from '../api'
import { detailId } from '../../../utils/detailId'

/**
 * The servers feature's reads: one page of the table, one server in full, and the picker the
 * subscription and stream forms draw.
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

/**
 * Every server as a picker entry - what the subscription and stream forms offer.
 *
 * Its own key and its own endpoint: the picker is gated by the subscription or stream read
 * grants, so a form that may be opened at all can fill it, and the table's page query is
 * neither needed nor permitted there.
 */
export const useAllServers = () =>
  useQuery({
    queryKey: ['servers', 'options'],
    queryFn: ({ signal }) => getServerOptions(signal),
  })
