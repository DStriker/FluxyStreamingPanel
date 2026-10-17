import { useQuery } from '@tanstack/react-query'
import { getActiveSessions, getAdminSessionHistory, getSessionHistory } from '../../../lib/api'
import type { SessionSortField, SessionSortOrder } from '../../../lib/api'

/**
 * The sessions feature's reads: the account's own visit history and active list, and the
 * administrator's installation-wide history.
 *
 * The keys live beside their hooks, which is why `['sessions', 'active']` is also the
 * literal `ActiveSessionsPage` names when a single revoke takes its card out of the
 * cache: a page may know a key it wrote through, and a key factory would be indirection
 * earning nothing until a third caller appears.
 */

/** One page of this account's visit history, keyed by everything the server filters by. */
export const useSessionHistory = (params: {
  page: number
  pageSize: number
  search?: string
  sortBy?: SessionSortField
  sortOrder?: SessionSortOrder
}) =>
  useQuery({
    queryKey: ['sessions', 'history', params],
    queryFn: ({ signal }) => getSessionHistory({ ...params, signal }),
  })

/** Every session this account currently holds. */
export const useActiveSessions = () =>
  useQuery({
    queryKey: ['sessions', 'active'],
    queryFn: ({ signal }) => getActiveSessions(signal),
  })

/** One page of the installation's visit history, keyed like the account's own. */
export const useAdminSessionHistory = (params: {
  page: number
  pageSize: number
  search?: string
  userId?: string | null
  sortBy?: SessionSortField
  sortOrder?: SessionSortOrder
}) =>
  useQuery({
    queryKey: ['adminSessions', params],
    queryFn: ({ signal }) => getAdminSessionHistory({ ...params, signal }),
  })
