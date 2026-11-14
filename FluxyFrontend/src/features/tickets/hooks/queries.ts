import { useQuery } from '@tanstack/react-query'
import { getAdminTicket, getAdminTickets, getTicket, getTickets } from '../api'
import type { AdminTicketQuery, TicketQuery } from '../api'
import { detailId } from '../../../utils/detailId'

/**
 * The tickets feature's reads: one page of each list and one detail from each side.
 *
 * One hook per endpoint with its key in this file beside it - the key *is* the parameters, so
 * a filter, a page turn or a different order is a new query rather than an effect that had to
 * notice one - and every `queryFn` passes React Query's `AbortSignal` through to `apiGet`.
 */

/** One page of the administrator's list, keyed by everything the server filters, sorts and pages by. */
export const useAdminTickets = (params: AdminTicketQuery) =>
  useQuery({
    queryKey: ['admin-tickets', params],
    queryFn: ({ signal }) => getAdminTickets({ ...params, signal }),
  })

/** One ticket in full as the administrator reads it. */
export const useAdminTicket = (id: string | undefined) =>
  useQuery({
    queryKey: ['admin-tickets', 'detail', id],
    queryFn: ({ signal }) => getAdminTicket(detailId(id), signal),
    enabled: id !== undefined,
  })

/** One page of the caller's own tickets. */
export const useTickets = (params: TicketQuery) =>
  useQuery({
    queryKey: ['tickets', params],
    queryFn: ({ signal }) => getTickets({ ...params, signal }),
  })

/** One of the caller's own tickets in full. */
export const useTicket = (id: string | undefined) =>
  useQuery({
    queryKey: ['tickets', 'detail', id],
    queryFn: ({ signal }) => getTicket(detailId(id), signal),
    enabled: id !== undefined,
  })
