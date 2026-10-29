import { useQuery } from '@tanstack/react-query'
import { getSubscription, getSubscriptions } from '../api'
import type { SubscriptionSortField, SubscriptionSortOrder } from '../api'
import type { SubscriptionStatus } from '../types'
import { detailId } from '../../../utils/detailId'

/**
 * The subscriptions feature's reads: one page of the table and one subscription in full.
 */

/**
 * One page of the subscriptions table.
 *
 * The key is the whole parameter set, so a search, a filter, an order or a page turn is a new
 * entry rather than an effect that had to notice one - the shape the streams table and the plans
 * table already use.
 */
export const useSubscriptions = (params: {
  page: number
  pageSize: number
  search: string
  ownerId?: string | null
  creatorId?: string | null
  status?: SubscriptionStatus | null
  sortBy: SubscriptionSortField
  sortOrder: SubscriptionSortOrder
}) =>
  useQuery({
    queryKey: ['subscriptions', params],
    queryFn: ({ signal }) => getSubscriptions({ ...params, signal }),
  })

/**
 * One subscription in full, for the edit form.
 *
 * Disabled while there is no identifier, which is what makes one component serve the add address
 * as well as the edit one - an add form must not ask for a subscription it is about to create.
 * `detailId` is what states that invariant in code rather than in a cast.
 */
export const useSubscription = (id: string | undefined) =>
  useQuery({
    queryKey: ['subscription', id],
    queryFn: ({ signal }) => getSubscription(detailId(id), signal),
    enabled: id !== undefined,
  })
