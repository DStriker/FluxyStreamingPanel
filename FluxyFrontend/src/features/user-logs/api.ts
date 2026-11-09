import { apiFetch, apiGet } from '../../utils/http'
import type { MessageResponse } from '../../types'
import type { UserLogRecordType, UserLogRowList } from './types'

/**
 * The user audit log: one page of it, and the whole-table clear.
 *
 * Two endpoints and nothing else, because the log is read and wiped. Nothing here writes an
 * entry - a row is written by the flow it describes, through the server's single fail-open
 * write path, and an operator editing the trail by hand would make it evidence of nothing.
 * There is no `editUserLogs` permission either: `viewUserLogs` gates both of these calls.
 */

/**
 * The columns the log may be ordered by, as the query-string spelling.
 *
 * Both are stored columns of the row. `username` is resolved for the page (it is the joined
 * account's current name, or the typed identifier recovered from the description), so an
 * order on it would order a page the server had already cut - which is why the table offers
 * two sortable headers and no more.
 */
export type UserLogSortField = 'createdAt' | 'recordType'

export type UserLogSortOrder = 'asc' | 'desc'

/** The page size the pager offers, which is also the server's default. */
export const USER_LOGS_PAGE_SIZE = 20

/**
 * The filters that narrow the list beyond the shared five parameters.
 *
 * All nine are optional. `userIds`, `groupIds` and `recordTypes` are **repeated** query
 * parameters - `?userIds=a&userIds=b` names two accounts - and each element is judged on its
 * own by the server: one bad element refuses the whole filter with a 400 under that
 * parameter's own name. That is why the page only ever writes values its pickers and its
 * read-back guards produced (`withFilterList` / `listParam`).
 *
 * `groupIds` matches the account's **current** group, so naming a group excludes rows about
 * unknown accounts - a row with no account belongs to no group.
 *
 * `ip`, `country` and `isp` are the connection log's three network filters, parameter for
 * parameter, because the shared `ConnectionNetworkCell` writes the same keys on either screen.
 * `ip` and `isp` keep the stored value exactly, case included; `country` is canonicalised by
 * the server to upper-case ISO alpha-2, so a hand-typed `de` means `DE`. A blank value is "no
 * filter", and a null column never matches.
 *
 * `dateFrom`/`dateTo` are one inclusive range over `createdAt`, each side optional, written
 * by the page's picker as the first/last millisecond of the chosen day in the account's zone
 * (see `dateRange.ts`).
 */
export interface UserLogFilters {
  /** Accounts to keep, or an empty/absent list for every account. */
  userIds?: readonly string[]
  /** Current groups of those accounts to keep, or an empty/absent list for every group. */
  groupIds?: readonly string[]
  /** Record types to keep, or an empty/absent list for every type. */
  recordTypes?: readonly UserLogRecordType[]
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
export interface UserLogQuery extends UserLogFilters {
  page: number
  pageSize: number
  search?: string
  sortBy: UserLogSortField
  sortOrder: UserLogSortOrder
}

/**
 * One page of the log: `GET /admin/user-logs`.
 *
 * Built with `URLSearchParams` for the reason every other list query is: a search term
 * containing `+` or `&` is searched for rather than reinterpreted, and an absent filter is
 * left off the request instead of being sent as the string `"null"`. The three list filters
 * are appended once per value - `set` would keep only the last, which would silently narrow
 * the table to one account of the chosen three - while the network filters are single values.
 */
export const getUserLogs = ({
  page,
  pageSize,
  search,
  userIds,
  groupIds,
  recordTypes,
  ip,
  country,
  isp,
  dateFrom,
  dateTo,
  sortBy,
  sortOrder,
  signal,
}: UserLogQuery & { signal?: AbortSignal }): Promise<UserLogRowList> => {
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

  for (const userId of userIds ?? []) query.append('userIds', userId)
  for (const groupId of groupIds ?? []) query.append('groupIds', groupId)
  for (const recordType of recordTypes ?? []) query.append('recordTypes', recordType)

  return apiGet<UserLogRowList>(`/admin/user-logs?${query.toString()}`, signal)
}

/**
 * Deletes every row of the log: `DELETE /admin/user-logs`.
 *
 * It sends **no filter and no identifier**, and that is the command: the button says "clear
 * the log", so the whole table goes whatever the reader had narrowed the screen to. The
 * endpoint ignores a query string on purpose, and repeating the call on an empty table is
 * the same success - the state the caller asked for is the state that holds.
 */
export const clearUserLogs = ({
  csrfToken,
}: {
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/user-logs', {
    method: 'DELETE',
    csrfToken,
  })
