import { apiFetch, apiGet } from '../../utils/http'
import type { MessageResponse } from '../../types'
import type { CreditLogList } from './types'

/**
 * The reseller credit log: one page of it, and the whole-table clear.
 *
 * Two endpoints and nothing else, because the log is read and wiped. Nothing here issues or
 * spends credit: those writes belong to the account create and update flows, which change the
 * balance and stage its log row in one save - a POST declared here would be a second way to
 * change a balance behind the account service's back.
 */

/**
 * The columns the log may be ordered by, as the query-string spelling.
 *
 * Both are columns of the ledger row. The reseller's username is joined for the page, so an
 * order on it would order a page the server had already cut - which is why the table offers
 * two sortable headers and no more.
 */
export type CreditLogSortField = 'createdAt' | 'amount'

export type CreditLogSortOrder = 'asc' | 'desc'

/** The page size the pager offers, which is also the server's default. */
export const CREDIT_LOGS_PAGE_SIZE = 20

/**
 * The filters that narrow the list beyond the shared five parameters.
 *
 * `resellerId` keeps one reseller's history; `dateFrom` and `dateTo` are one inclusive range
 * over `created_at`, each side optional. The two bounds are written by the toolbar's picker
 * as the first/last millisecond of the chosen day in the account's zone (see
 * `dateRange.ts`), and a single-sided range leaves the other side absent.
 */
export interface CreditLogFilters {
  /** Reseller to keep, or null/absent for every reseller. */
  resellerId?: string | null
  /** Earliest creation instant to keep, inclusive, as an ISO 8601 date-time. */
  dateFrom?: string | null
  /** Latest creation instant to keep, inclusive, as an ISO 8601 date-time. */
  dateTo?: string | null
}

/** Everything one page request carries. */
export interface CreditLogQuery extends CreditLogFilters {
  page: number
  pageSize: number
  search?: string
  sortBy: CreditLogSortField
  sortOrder: CreditLogSortOrder
}

/**
 * One page of the log: `GET /admin/credit-logs`.
 *
 * Built with `URLSearchParams` for the reason every other list query is: a search term
 * containing `+` or `&` is searched for rather than reinterpreted, and an absent filter is
 * left off the request instead of being sent as the string `"null"`.
 */
export const getCreditLogs = ({
  page,
  pageSize,
  search,
  resellerId,
  dateFrom,
  dateTo,
  sortBy,
  sortOrder,
  signal,
}: CreditLogQuery & { signal?: AbortSignal }): Promise<CreditLogList> => {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    sortBy,
    sortOrder,
  })

  if (search) query.set('search', search)
  if (resellerId) query.set('resellerId', resellerId)
  if (dateFrom) query.set('dateFrom', dateFrom)
  if (dateTo) query.set('dateTo', dateTo)

  return apiGet<CreditLogList>(`/admin/credit-logs?${query.toString()}`, signal)
}

/**
 * Deletes every row of the log: `DELETE /admin/credit-logs`.
 *
 * It sends **no filter and no identifier**, and that is the command: the button says "clear
 * all logs", so the whole table goes whatever the reader had narrowed the screen to.
 * Repeating it on an empty table is the same success - the state the caller asked for is the
 * state that holds.
 */
export const clearCreditLogs = ({
  csrfToken,
}: {
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/credit-logs', {
    method: 'DELETE',
    csrfToken,
  })
