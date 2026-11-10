import { apiFetch, apiGet } from '../../utils/http'
import type { MessageResponse } from '../../types'
import type { ApiRequestFailureReason, ApiRequestFailureRowList } from './types'

/**
 * The API request failure log: one page of it, and the whole-table clear.
 *
 * Two endpoints and nothing else, because the log is read and wiped. Nothing here records a
 * refusal - this capability has no production write path at all, so `POST`, `PUT` and `PATCH`
 * on the collection answer 405 - and an operator editing the record of refusals by hand would
 * make it evidence of nothing. There is no `editApiRequestFailures` permission either:
 * `viewApiRequestFailures` gates both of these calls, because the wipe destroys the same
 * evidence.
 */

/**
 * The columns the log may be ordered by, as the query-string spelling.
 *
 * Both are stored columns of the row, which is the whole rule: nothing joined or resolved is
 * ever sortable, because an order applied to a page the server had already cut would order
 * nothing but that page.
 */
export type ApiRequestFailureSortField = 'createdAt' | 'reason'

export type ApiRequestFailureSortOrder = 'asc' | 'desc'

/** The page size the pager offers, which is also the server's default. */
export const API_REQUEST_FAILURES_PAGE_SIZE = 20

/**
 * The filters that narrow the list beyond the shared five parameters.
 *
 * All six are optional. `reasons` is a **repeated** query parameter - `?reasons=auth_failed&
 * reasons=user_blocked` names two reasons - and each element is judged on its own by the
 * server: one bad element refuses the whole filter with a 400 under that parameter's own
 * name. That is why the page only ever writes values its picker and read-back guard produced
 * (`withFilterList` / `listParam`).
 *
 * `ip`, `country` and `isp` are the connection log's three network filters, parameter for
 * parameter, because the shared `ConnectionNetworkCell` writes the same keys on either
 * screen. `ip` and `isp` keep the stored value exactly, case included; `country` is
 * canonicalised by the server to upper-case ISO alpha-2, so a hand-typed `de` means `DE`. A
 * blank value is "no filter", and a null column never matches.
 *
 * `dateFrom` and `dateTo` are one inclusive range over `created_at`, each side optional -
 * the credit and user logs' precedent. The picker writes the first/last millisecond of the
 * chosen day in the account's zone (see `dateRange.ts`).
 */
export interface ApiRequestFailureFilters {
  /** Reasons to keep, or an empty/absent list for every reason. */
  reasons?: readonly ApiRequestFailureReason[]
  /** Address to keep, compared exactly. */
  ip?: string | null
  /** Upper-case ISO alpha-2 country. */
  country?: string | null
  /** Provider name, compared exactly. */
  isp?: string | null
  /** Earliest creation instant to keep, inclusive, as an ISO 8601 date-time. */
  dateFrom?: string | null
  /** Latest creation instant to keep, inclusive, as an ISO 8601 date-time. */
  dateTo?: string | null
}

/** Everything one page request carries. */
export interface ApiRequestFailureQuery extends ApiRequestFailureFilters {
  page: number
  pageSize: number
  search?: string
  sortBy: ApiRequestFailureSortField
  sortOrder: ApiRequestFailureSortOrder
}

/**
 * One page of the log: `GET /admin/api-request-failures`.
 *
 * Built with `URLSearchParams` for the reason every other list query is: a search term
 * containing `+` or `&` is searched for rather than reinterpreted, and an absent filter is
 * left off the request instead of being sent as the string `"null"`. The repeated `reasons`
 * parameter is appended once per value - `set` would keep only the last, which would
 * silently narrow the table to one reason of the chosen three - while the network filters are
 * single values.
 */
export const getApiRequestFailures = ({
  page,
  pageSize,
  search,
  reasons,
  ip,
  country,
  isp,
  dateFrom,
  dateTo,
  sortBy,
  sortOrder,
  signal,
}: ApiRequestFailureQuery & { signal?: AbortSignal }): Promise<ApiRequestFailureRowList> => {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    sortBy,
    sortOrder,
  })

  if (search) query.set('search', search)
  if (ip) query.set('ip', ip)
  if (country) query.set('country', country)
  if (isp) query.set('isp', isp)
  if (dateFrom) query.set('dateFrom', dateFrom)
  if (dateTo) query.set('dateTo', dateTo)

  for (const reason of reasons ?? []) query.append('reasons', reason)

  return apiGet<ApiRequestFailureRowList>(`/admin/api-request-failures?${query.toString()}`, signal)
}

/**
 * Deletes every row of the log: `DELETE /admin/api-request-failures`.
 *
 * It sends **no filter and no identifier**, and that is the command: the button says "clear
 * the log", so the whole table goes whatever the reader had narrowed the screen to. The
 * endpoint ignores a query string on purpose, and repeating the call on an empty table is the
 * same success - the state the caller asked for is the state that holds.
 */
export const clearApiRequestFailures = ({
  csrfToken,
}: {
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/api-request-failures', {
    method: 'DELETE',
    csrfToken,
  })
