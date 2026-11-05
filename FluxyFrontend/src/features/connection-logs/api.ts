import { apiFetch, apiGet } from '../../utils/http'
import type { MessageResponse } from '../../types'
import type { ConnectionLogList, ConnectionStreamKind } from './types'

/**
 * The connection log: one page of it, and the whole-table clear.
 *
 * Two endpoints and nothing else, because the log is read and wiped. Nothing here writes a
 * row: the capture path belongs to the process that relays a stream, and it is not this API.
 */

/**
 * The columns the log may be ordered by, as the query-string spelling.
 *
 * Both are stored timestamps, and both are the endpoint's own sort fields - which is why the
 * table keys its two sortable columns by these names and no mapping table exists to get
 * wrong. The owner's name and the stream's title are resolved for the page, so an order on
 * either would order a page the server had already cut.
 */
export type ConnectionLogSortField = 'startedAt' | 'endedAt'

export type ConnectionLogSortOrder = 'asc' | 'desc'

/**
 * Whether the list keeps only the sessions that have not ended, or all of them.
 *
 * `live` is `endedAt IS NULL` and nothing else, which is the same definition the endpoint
 * applies and the same one its partial index is built on.
 */
export type ConnectionLogState = 'live' | 'all'

/** The page size the pager offers, which is also the server's default. */
export const CONNECTION_LOGS_PAGE_SIZE = 20

/**
 * The filters that narrow the list beyond the shared five parameters.
 *
 * All eight are optional and each is a value the endpoint compares exactly, except `search`,
 * which is the shared free-text box. `country` is stored upper case on the row and matched
 * case-insensitively by the server; `userAgent` is matched exactly, case included. The two
 * start-time bounds narrow the row's own `started_at` and are inclusive on both ends.
 */
export interface ConnectionLogFilters {
  /** Subscription to keep, or null/absent for every subscription. */
  subscriptionId?: string | null
  /** Owner of that subscription, or null/absent for every owner. */
  ownerId?: string | null
  /** Opaque stream identifier, or null/absent for every stream. */
  streamId?: string | null
  /** Address, compared exactly. */
  ip?: string | null
  /** Upper-case ISO alpha-2 country. */
  country?: string | null
  /** Provider name, compared exactly. */
  isp?: string | null
  /**
   * User agent, compared exactly and case included.
   *
   * Case included because it is a client's own string: `CURL/8.5.0` and `curl/8.5.0` are two
   * different clients as far as the log is concerned, and folding one into the other would answer
   * rows for a client that never sent that string. The same rule the address and the provider
   * follow beside it.
   */
  userAgent?: string | null
  /**
   * Earliest start instant to keep, inclusive, as an ISO 8601 date-time.
   *
   * Written by the toolbar's range picker as the first millisecond of the chosen day in the
   * account's zone (see `startRange.ts`); absent means no lower bound.
   */
  startedFrom?: string | null
  /**
   * Latest start instant to keep, inclusive, as an ISO 8601 date-time.
   *
   * The last millisecond of the chosen day in the account's zone, so the day the operator
   * picked as the end is not lost; absent means no upper bound.
   */
  startedTo?: string | null
}

/** Everything one page request carries. */
export interface ConnectionLogQuery extends ConnectionLogFilters {
  /** Medium to keep - the endpoint always names exactly one. */
  kind: ConnectionStreamKind
  /** Which sessions the state keeps. */
  state: ConnectionLogState
  page: number
  pageSize: number
  search?: string
  sortBy: ConnectionLogSortField
  sortOrder: ConnectionLogSortOrder
}

/**
 * One page of the log: `GET /admin/connection-logs`.
 *
 * Built with `URLSearchParams` for the reason every other list query is: a search term
 * containing `+` or `&` is searched for rather than reinterpreted, and an absent filter is
 * left off the request instead of being sent as the string `"null"`.
 */
export const getConnectionLogs = ({
  kind,
  state,
  page,
  pageSize,
  search,
  subscriptionId,
  ownerId,
  streamId,
  ip,
  country,
  isp,
  userAgent,
  startedFrom,
  startedTo,
  sortBy,
  sortOrder,
  signal,
}: ConnectionLogQuery & { signal?: AbortSignal }): Promise<ConnectionLogList> => {
  const query = new URLSearchParams({
    kind,
    state,
    page: String(page),
    pageSize: String(pageSize),
    sortBy,
    sortOrder,
  })

  if (search) query.set('search', search)
  if (subscriptionId) query.set('subscriptionId', subscriptionId)
  if (ownerId) query.set('ownerId', ownerId)
  if (streamId) query.set('streamId', streamId)
  if (ip) query.set('ip', ip)
  if (country) query.set('country', country)
  if (isp) query.set('isp', isp)
  if (userAgent) query.set('userAgent', userAgent)
  if (startedFrom) query.set('startedFrom', startedFrom)
  if (startedTo) query.set('startedTo', startedTo)

  return apiGet<ConnectionLogList>(`/admin/connection-logs?${query.toString()}`, signal)
}

/**
 * Deletes every row of the log: `DELETE /admin/connection-logs`.
 *
 * It sends **no filter and no identifier**, and that is the command: the button says "clear all
 * logs", so the whole table goes whatever the reader had narrowed the screen to. Repeating it
 * on an empty table is the same success - the state the caller asked for is the state that
 * holds.
 */
export const clearConnectionLogs = ({
  csrfToken,
}: {
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/connection-logs', {
    method: 'DELETE',
    csrfToken,
  })
