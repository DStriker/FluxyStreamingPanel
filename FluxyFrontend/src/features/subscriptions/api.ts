import { apiFetch, apiGet } from '../../utils/http'
import type { MessageResponse } from '../../types'
import type {
  SubscriptionDetail,
  SubscriptionList,
  SubscriptionOutputFormat,
  SubscriptionStatus,
} from './types'

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

/** Every setting a subscription carries - the body of both writes. */
export interface SubscriptionValues {
  /** Owner, only sent on create; the server ignores it on an update. */
  userId: string | null
  /** The playlist service's identifier, or null. */
  playlistId: string | null
  /** Expiry as `yyyy-MM-dd`, or null for "never expires". */
  expireAt: string | null
  /** The tariff plan it is sold under, or null. */
  tariffPlanId: string | null
  /** True to clear the plan rather than leave the reference alone. */
  clearTariffPlan: boolean
  /** Connection ceiling, or null for unlimited. */
  maxConnections: number | null
  /** True to store unlimited connections rather than a ceiling. */
  clearMaxConnections: boolean
  /** Whether the subscriber may restream. */
  isRestreamer: boolean
  /** Addresses it may be watched from. Empty means no restriction. */
  ipWhitelist: string[]
  /** User agents it may be watched with. Empty means no restriction. */
  userAgentWhitelist: string[]
  /** Whether it is pinned to one provider. */
  isIspLock: boolean
  /** Whether it is pinned to one country. */
  isCountryLock: boolean
  /** Whether it is a trial. */
  isTrial: boolean
  /** The formats it is delivered in. At least one. */
  outputFormats: SubscriptionOutputFormat[]
  /** MAG device's MAC, or null. */
  magMac: string | null
  /** Whether MAG identifiers are checked strictly. */
  isStbLock: boolean
  /** Enigma2 device's MAC, or null. */
  enigma2Mac: string | null
  /** The bouquets it carries while no plan is attached. */
  bouquetIds: string[]
}

/**
 * The body a create sends: every field, with the two "clear" flags left out.
 *
 * A create has nothing to clear, so the two flags would be a statement about a row that does not
 * exist yet. The owner travels because a create is the one request that chooses it.
 */
const toCreateBody = (values: SubscriptionValues) => ({
  userId: values.userId,
  playlistId: values.playlistId,
  expireAt: values.expireAt,
  tariffPlanId: values.tariffPlanId,
  maxConnections: values.maxConnections,
  isRestreamer: values.isRestreamer,
  ipWhitelist: values.ipWhitelist,
  userAgentWhitelist: values.userAgentWhitelist,
  isIspLock: values.isIspLock,
  isCountryLock: values.isCountryLock,
  isTrial: values.isTrial,
  outputFormats: values.outputFormats,
  magMac: values.magMac,
  isStbLock: values.isStbLock,
  enigma2Mac: values.enigma2Mac,
  bouquetIds: values.bouquetIds,
})

/**
 * The body an update sends.
 *
 * The whole form travels rather than a diff, for the reason every other form here does: one
 * screen holds one subscription, so what the operator sees and what the row becomes are the same
 * statement. The two "clear" flags are what let a nullable field say "set to nothing" apart from
 * "leave it alone", which a plain null cannot.
 */
const toUpdateBody = (values: SubscriptionValues) => ({
  playlistId: values.playlistId,
  expireAt: values.expireAt,
  tariffPlanId: values.tariffPlanId,
  clearTariffPlan: values.clearTariffPlan && values.tariffPlanId === null,
  maxConnections: values.maxConnections,
  clearMaxConnections: values.clearMaxConnections && values.maxConnections === null,
  isRestreamer: values.isRestreamer,
  ipWhitelist: values.ipWhitelist,
  userAgentWhitelist: values.userAgentWhitelist,
  isIspLock: values.isIspLock,
  isCountryLock: values.isCountryLock,
  isTrial: values.isTrial,
  outputFormats: values.outputFormats,
  magMac: values.magMac,
  isStbLock: values.isStbLock,
  enigma2Mac: values.enigma2Mac,
  bouquetIds: values.bouquetIds,
})

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
