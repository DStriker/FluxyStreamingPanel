import { apiFetch, apiGet } from '../../utils/http'
import type { MessageResponse } from '../../types'
import type { BlacklistEntry, BlacklistEntryList, BlacklistType } from './types'

/**
 * The three access-control blacklists: one page of one list, one entry in full, and the three
 * writes.
 *
 * One endpoint family serves all three lists, because they are one resource: the list is the
 * `type` parameter, and the three screens differ in nothing but the constant they send. The
 * counter and the added date have no endpoint here - the enforcement increments the first, and
 * the second is stamped once when the row is inserted.
 */

/**
 * The columns the table may be ordered by, as the query-string spelling.
 *
 * These are the endpoint's own sort fields. The column keys of the page are these same names,
 * so a header click hands its key straight to `getBlacklistEntries` with no mapping table to
 * get wrong.
 */
export type BlacklistSortField = 'value' | 'blockedAttempts' | 'createdAt'

export type BlacklistSortOrder = 'asc' | 'desc'

/** The page size the pager offers, which is also the server's default. */
export const BLACKLISTS_PAGE_SIZE = 20

/** The value and note limits, mirrored from the server so the dialog can refuse before the wire. */
export const BLACKLIST_VALUE_MAX = 512
export const BLACKLIST_NOTE_MAX = 1000

/** Everything the add/edit dialog collects - the body of both writes. */
export interface BlacklistEntryValues {
  /** The value that is refused. */
  value: string
  /** Free-text note, or an empty string for none. */
  note: string
}

/**
 * The body both writes send: the value trimmed, and the note trimmed into null when it is
 * blank.
 *
 * A `PATCH` distinguishes "kept" from "cleared" by the note being absent or an empty string, so
 * the mapping is stated here once rather than at the call site.
 */
const toBody = (values: BlacklistEntryValues) => ({
  value: (values.value ?? '').trim(),
  note: values.note ?? '',
})

/** One page of one list: `GET /admin/blacklist-entries?type=…`. */
export const getBlacklistEntries = ({
  type,
  page,
  pageSize,
  search,
  sortBy,
  sortOrder,
  signal,
}: {
  type: BlacklistType
  page: number
  pageSize: number
  search?: string
  sortBy: BlacklistSortField
  sortOrder: BlacklistSortOrder
  signal?: AbortSignal
}): Promise<BlacklistEntryList> => {
  const query = new URLSearchParams({
    type,
    page: String(page),
    pageSize: String(pageSize),
    sortBy,
    sortOrder,
  })

  if (search) query.set('search', search)

  return apiGet<BlacklistEntryList>(`/admin/blacklist-entries?${query.toString()}`, signal)
}

/** One entry in full: `GET /admin/blacklist-entries/{id}`. */
export const getBlacklistEntry = (id: string, signal?: AbortSignal): Promise<BlacklistEntry> =>
  apiGet<BlacklistEntry>(`/admin/blacklist-entries/${encodeURIComponent(id)}`, signal)

/** Creates an entry in one list: `POST /admin/blacklist-entries`, answered with `201` and a `Location`. */
export const createBlacklistEntry = ({
  type,
  values,
  csrfToken,
}: {
  type: BlacklistType
  values: BlacklistEntryValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/blacklist-entries', {
    method: 'POST',
    body: { type, ...toBody(values) },
    csrfToken,
  })

/**
 * Changes the value and the note of an entry: `PATCH /admin/blacklist-entries/{id}`.
 *
 * The type is not sent: it is what the screen already is, and the server does not accept a
 * change to it.
 */
export const updateBlacklistEntry = ({
  id,
  values,
  csrfToken,
}: {
  id: string
  values: BlacklistEntryValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/blacklist-entries/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: toBody(values),
    csrfToken,
  })

/** Deletes an entry: `DELETE /admin/blacklist-entries/{id}`. */
export const deleteBlacklistEntry = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/blacklist-entries/${encodeURIComponent(id)}`, {
    method: 'DELETE',
    csrfToken,
  })

/**
 * Empties one list: `DELETE /admin/blacklist-entries?type=…`.
 *
 * It names the list and nothing else. A search the operator has applied is a property of the
 * view, not of the command: sending it would remove the visible rows and leave the screen
 * looking empty with rows still behind it, which is exactly the state the confirmation exists
 * to prevent. The server answers the same success for an already-empty list.
 */
export const clearBlacklistEntries = ({
  type,
  csrfToken,
}: {
  type: BlacklistType
  csrfToken: string | null
}): Promise<MessageResponse> => {
  const query = new URLSearchParams({ type })

  return apiFetch(`/admin/blacklist-entries?${query.toString()}`, {
    method: 'DELETE',
    csrfToken,
  })
}
