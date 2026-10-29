import { apiFetch, apiGet } from '../../utils/http'
import type { MessageResponse } from '../../types'
import type { SubscriptionDetail, SubscriptionList, SubscriptionStatus } from './types'
import { toCreateBody, toUpdateBody } from './subscriptionBody'
import type { SubscriptionValues } from './subscriptionBody'

export type { SubscriptionValues } from './subscriptionBody'

/**
 * The subscriptions: the rows an operator issues, the page of them the table draws, and the
 * writes its form and row actions send.
 *
 * The domain has three shapes and this module keeps them apart. A list row is what the table
 * draws, including the *effective* service support its subrows decide their "add a service"
 * buttons from. The detail is what the edit form reads - the stored values plus the plan-derived
 * block. `SubscriptionValues` is what either write sends.
 */

/** The columns the table may be ordered by, as the query-string spelling. */
export type SubscriptionSortField = 'createdAt' | 'expireAt'

export type SubscriptionSortOrder = 'asc' | 'desc'

/** The page size the pager opens with, which is also the server's default. */
export const SUBSCRIPTIONS_PAGE_SIZE = 20

/**
 * The seven collective operations the bulk endpoint accepts.
 *
 * The first three are the ones a selection is collectivised for - move it to another plan, hand it
 * to another issuer, remove it - and the four after them are the per-field clears a row would
 * otherwise have to be opened for.
 */
export type BulkSubscriptionAction =
  | 'change-tariff'
  | 'change-creator'
  | 'delete'
  | 'delete-playlist'
  | 'delete-mag'
  | 'delete-enigma2'
  | 'block-owner'

/** The body a bulk run sends: the action, the ids and the one value it writes. */
export interface BulkSubscriptionsPayload {
  action: BulkSubscriptionAction
  ids: string[]
  tariffPlanId?: string
  resellerId?: string
}

/**
 * One page of the table: `GET /admin/subscriptions`.
 *
 * A plain read behind the admin session, so no antiforgery token - nothing is changed. The page,
 * the search, the three filters and the order are the server's because the pager draws a `total`
 * and the status is computed: a filter applied here would narrow the rows on screen while the
 * pager still counted everything, and the tag would be an answer the filter could not reproduce.
 */
export const getSubscriptions = ({
  page,
  pageSize,
  search,
  ownerId,
  creatorId,
  status,
  sortBy,
  sortOrder,
  signal,
}: {
  page: number
  pageSize: number
  search?: string
  ownerId?: string | null
  creatorId?: string | null
  status?: SubscriptionStatus | null
  sortBy: SubscriptionSortField
  sortOrder: SubscriptionSortOrder
  signal?: AbortSignal
}): Promise<SubscriptionList> => {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    sortBy,
    sortOrder,
  })

  if (search) query.set('search', search)
  if (ownerId) query.set('ownerId', ownerId)
  if (creatorId) query.set('creatorId', creatorId)
  if (status) query.set('status', status)

  return apiGet<SubscriptionList>(`/admin/subscriptions?${query.toString()}`, signal)
}

/** One subscription in full, for the edit form: `GET /admin/subscriptions/{id}`. */
export const getSubscription = (id: string, signal?: AbortSignal): Promise<SubscriptionDetail> =>
  apiGet<SubscriptionDetail>(`/admin/subscriptions/${encodeURIComponent(id)}`, signal)

/**
 * Issues a subscription: `POST /admin/subscriptions`, answered with `201 subscription_created`
 * and a `Location` naming it.
 *
 * A playlist id or MAC another subscription holds is `409 subscription_already_exists` with the
 * field named in `errors`, so the form shows it under the input rather than as a toast.
 */
export const createSubscription = ({
  values,
  csrfToken,
}: {
  values: SubscriptionValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/subscriptions', {
    method: 'POST',
    body: toCreateBody(values),
    csrfToken,
  })

/** Changes a subscription: `PATCH /admin/subscriptions/{id}`. */
export const updateSubscription = ({
  id,
  values,
  csrfToken,
}: {
  id: string
  values: SubscriptionValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/subscriptions/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: toUpdateBody(values),
    csrfToken,
  })

/** Deletes a subscription: `DELETE /admin/subscriptions/{id}`. */
export const deleteSubscription = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/subscriptions/${encodeURIComponent(id)}`, { method: 'DELETE', csrfToken })

/**
 * The subset of a subscription a row strip may change in place.
 *
 * A strip edits one service and knows nothing about the rest of the row - the whitelists, the two
 * lock flags and the bouquet set are not on the list at all - so it sends only the field it
 * touched. `SubscriptionPatch` treats an absent property as "keep" and an empty string as "clear",
 * which is exactly the shape this needs and the reason it is a separate call rather than
 * `updateSubscription` with a reconstructed body: a reconstruction that guessed the fields it did
 * not know would clear them.
 */
export interface SubscriptionFieldPatch {
  playlistId?: string
  magMac?: string
  enigma2Mac?: string
}

/** Changes one service field of a subscription, leaving every other field as it is. */
export const patchSubscription = ({
  id,
  patch,
  csrfToken,
}: {
  id: string
  patch: SubscriptionFieldPatch
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/subscriptions/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: patch,
    csrfToken,
  })

/**
 * Mints a playlist id no subscription holds yet, without writing anything.
 *
 * A recorded departure from the REST shape of the resource: it is a POST because it mints a value
 * the create form and the regenerate action need to show before saving, and it writes no row. It
 * carries the antiforgery pair because it mints something the operator is about to store.
 */
export const generatePlaylistId = ({
  csrfToken,
}: {
  csrfToken: string | null
}): Promise<{ playlistId: string }> =>
  apiFetch('/admin/subscriptions/playlist-id', { method: 'POST', body: {}, csrfToken })

/** Runs one operation over a selection: `POST /admin/subscriptions/bulk`. */
export const bulkSubscriptions = ({
  payload,
  csrfToken,
}: {
  payload: BulkSubscriptionsPayload
  csrfToken: string | null
}): Promise<import('../../types').BulkOperationResponse> =>
  apiFetch('/admin/subscriptions/bulk', { method: 'POST', body: payload, csrfToken })
