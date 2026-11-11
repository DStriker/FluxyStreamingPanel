import { apiFetch, apiGet } from '../../utils/http'
import type { MessageResponse } from '../../types'
import type { MagEventRowList, MagEventType } from './types'

/**
 * The MAG event journal: one page of it, one event sent to a device, and the whole-table
 * clear.
 *
 * Three endpoints, because the journal has one write that is not a command: a send records a
 * new row for one subscription's device. Nothing here edits or removes a row - an event is a
 * fact that was sent, and the collection's `PATCH`/`PUT` and its `{id}` methods answer 405.
 *
 * Reading the journal is reading the subscription surface (`viewSubscriptions`); sending an
 * event and wiping the journal are changes to what that surface holds (`editSubscriptions`).
 * The path mirrors the backend's own route (`admin/mag-events`), so the page, the API and the
 * menu entry name one resource the same way.
 */

/** The one column the journal may be ordered by, as the query-string spelling. */
export type MagEventSortField = 'createdAt'

export type MagEventSortOrder = 'asc' | 'desc'

/** The page size the pager offers, which is also the server's default. */
export const MAG_EVENTS_PAGE_SIZE = 20

/**
 * The filters that narrow the list beyond the shared five parameters.
 *
 * All four are optional. `search` matches the stored message **or** the device MAC - the two
 * facts a support call starts from - and is applied by the server. `event` is one of the six
 * wire names; the server refuses an unknown value under that parameter's own name rather than
 * widening the list, so the page only ever writes values its picker produced. `subscriptionId`
 * is a uuid as text, written by a drill-down from the subscriptions table.
 *
 * `dateFrom` and `dateTo` are one inclusive range over `createdAt`, each side optional - the
 * other logs' precedent. The picker writes the first/last millisecond of the chosen day in the
 * account's zone (see `dateRange.ts`).
 */
export interface MagEventFilters {
  /** Only events of this operation, or null. */
  event?: MagEventType | null
  /** Only events of this subscription, as text, or null. */
  subscriptionId?: string | null
  /** Earliest creation instant to keep, inclusive, as an ISO 8601 date-time. */
  dateFrom?: string | null
  /** Latest creation instant to keep, inclusive, as an ISO 8601 date-time. */
  dateTo?: string | null
}

/** Everything one page request carries. */
export interface MagEventQuery extends MagEventFilters {
  page: number
  pageSize: number
  search?: string
  sortBy: MagEventSortField
  sortOrder: MagEventSortOrder
}

/**
 * One page of the journal: `GET /admin/mag-events`.
 *
 * Built with `URLSearchParams` for the reason every other list query is: a search term
 * containing `+` or `&` is searched for rather than reinterpreted, and an absent filter is
 * left off the request instead of being sent as the string `"null"`.
 */
export const getMagEvents = ({
  page,
  pageSize,
  search,
  event,
  subscriptionId,
  dateFrom,
  dateTo,
  sortBy,
  sortOrder,
  signal,
}: MagEventQuery & { signal?: AbortSignal }): Promise<MagEventRowList> => {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    sortBy,
    sortOrder,
  })

  if (search) query.set('search', search)
  if (event) query.set('event', event)
  if (subscriptionId) query.set('subscriptionId', subscriptionId)
  if (dateFrom) query.set('dateFrom', dateFrom)
  if (dateTo) query.set('dateTo', dateTo)

  return apiGet<MagEventRowList>(`/admin/mag-events?${query.toString()}`, signal)
}

/**
 * What the send form hands over: the subscription, the operation and its payload.
 *
 * No flag of any kind travels - `needConfirm`, `rebootAfterOk`, `autoHideTimeout`,
 * `additionalServicesOn`, `anec` and `vclub` are the server's to resolve, and a client that
 * sent them would be inventing defaults the server has deliberately kept out of the database.
 * `message` is present only for the two operations that carry one; the body drops a message
 * sent for the other four on the server's side anyway.
 */
export interface MagEventSendBody {
  subscriptionId: string
  event: MagEventType
  message?: string
}

/**
 * Sends one event to the MAG device of a subscription: `POST /admin/mag-events`.
 *
 * Answers 201 with `mag_event_sent`, `validation_failed` with `errors` keyed `message`,
 * `subscriptionId` or `event`, and `mag_device_missing` when the subscription has no device.
 * The antiforgery token is minted at call time by `useWrite`, never carried here.
 */
export const sendMagEvent = ({
  csrfToken,
  ...body
}: MagEventSendBody & {
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/mag-events', {
    method: 'POST',
    body,
    csrfToken,
  })

/**
 * Deletes every row of the journal: `DELETE /admin/mag-events`.
 *
 * It sends **no filter and no identifier**, and that is the command: the button says "clear
 * the journal", so the whole table goes whatever the reader had narrowed the screen to. The
 * endpoint ignores a query string on purpose, and repeating the call on an empty table is the
 * same success - the state the caller asked for is the state that holds.
 */
export const clearMagEvents = ({
  csrfToken,
}: {
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/mag-events', {
    method: 'DELETE',
    csrfToken,
  })
