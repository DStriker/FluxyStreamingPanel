import { apiFetch, apiGet } from '../../utils/http'
import type { MessageResponse } from '../../types'
import type { StreamProblemRowList } from './types'

/**
 * The stream problem journal: one page of it, and the whole-table clear.
 *
 * Two endpoints and nothing else, because the journal is read and wiped. Nothing here records
 * a problem - a row is written by the future stream producer through the backend's own service
 * - so `POST`, `PUT`, `PATCH` and `DELETE {id}` on the collection answer 405. The read sits on
 * `viewStreams` and the clear on `editStreams`: reading the streams' history is the parent
 * resource's read, and wiping the record of their failures is a change to what the streams
 * surface holds.
 */

/** The one column the journal may be ordered by, as the query-string spelling. */
export type StreamProblemSortField = 'createdAt'

export type StreamProblemSortOrder = 'asc' | 'desc'

/** The page size the pager offers, which is also the server's default. */
export const STREAM_PROBLEMS_PAGE_SIZE = 20

/**
 * The filters that narrow the list beyond the shared five parameters.
 *
 * All four are optional. `search` matches the stored error message only - the stream's name is
 * resolved through a join on read and is deliberately not part of the stored predicate.
 * `streamId` is a uuid as text; the server refuses one that is not a uuid under that
 * parameter's own name, so the page only ever writes values its stream picker produced.
 *
 * `dateFrom` and `dateTo` are one inclusive range over `createdAt`, each side optional - the
 * credit and user logs' precedent. The picker writes the first/last millisecond of the chosen
 * day in the account's zone (see `dateRange.ts`).
 */
export interface StreamProblemFilters {
  /** Only problems of this stream, as text, or null. */
  streamId?: string | null
  /** Earliest creation instant to keep, inclusive, as an ISO 8601 date-time. */
  dateFrom?: string | null
  /** Latest creation instant to keep, inclusive, as an ISO 8601 date-time. */
  dateTo?: string | null
}

/** Everything one page request carries. */
export interface StreamProblemQuery extends StreamProblemFilters {
  page: number
  pageSize: number
  search?: string
  sortBy: StreamProblemSortField
  sortOrder: StreamProblemSortOrder
}

/**
 * One page of the journal: `GET /admin/stream-problems`.
 *
 * Built with `URLSearchParams` for the reason every other list query is: a search term
 * containing `+` or `&` is searched for rather than reinterpreted, and an absent filter is
 * left off the request instead of being sent as the string `"null"`.
 */
export const getStreamProblems = ({
  page,
  pageSize,
  search,
  streamId,
  dateFrom,
  dateTo,
  sortBy,
  sortOrder,
  signal,
}: StreamProblemQuery & { signal?: AbortSignal }): Promise<StreamProblemRowList> => {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    sortBy,
    sortOrder,
  })

  if (search) query.set('search', search)
  if (streamId) query.set('streamId', streamId)
  if (dateFrom) query.set('dateFrom', dateFrom)
  if (dateTo) query.set('dateTo', dateTo)

  return apiGet<StreamProblemRowList>(`/admin/stream-problems?${query.toString()}`, signal)
}

/**
 * Deletes every row of the journal: `DELETE /admin/stream-problems`.
 *
 * It sends **no filter and no identifier**, and that is the command: the button says "clear
 * the journal", so the whole table goes whatever the reader had narrowed the screen to. The
 * endpoint ignores a query string on purpose, and repeating the call on an empty table is the
 * same success - the state the caller asked for is the state that holds.
 */
export const clearStreamProblems = ({
  csrfToken,
}: {
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/stream-problems', {
    method: 'DELETE',
    csrfToken,
  })
