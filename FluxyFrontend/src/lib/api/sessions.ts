import { apiFetch, apiGet } from '../http'
import type {
  ActiveSessionList,
  AdminSessionHistoryResponse,
  MessageResponse,
  SessionHistoryResponse,
} from '../../types'

/**
 * Both halves of the session story: the visit history an account reads about itself, the
 * installation's history an administrator reads, and the list of sign-ins that are still
 * running with the way to end one.
 *
 * The two histories share their paging contract, and the two "end this session" endpoints
 * share theirs: an id names the session rather than the row, and one answer covers both an
 * unknown id and one outside the caller's reach.
 */

/**
 * What a page of the visit history is ordered by, spelled the way the endpoint reads it.
 *
 * Only two values exist because only two columns do: the moment and the address. Country and
 * provider are resolved from GeoIP while a page is being read rather than stored, so there is
 * nothing for the server to sort on and a third option here would be a promise with no row
 * behind it.
 */
export type SessionSortField = 'visitedAt' | 'ip'

/** Which way a page runs. `desc` by default - the newest visit is what the page is for. */
export type SessionSortOrder = 'asc' | 'desc'

/**
 * One page of this account's visit history, ordered and filtered by the server.
 *
 * The bounds are enforced by the server rather than here: a page number is read off the pager,
 * and a pager that offered 500 rows would be refused with `validation_failed` on `pageSize`
 * instead of silently receiving fewer than it asked for. Like every other GET this carries no
 * antiforgery token - the call changes nothing, so there is no state for a cross-site submission
 * to alter - and it reads only the caller's own history, because the account comes from the
 * token rather than from a parameter.
 *
 * The search and the sort are parameters of the request rather than something the page does to
 * the rows it already has. Filtering or ordering client side would apply to one page only while
 * `total` still counted everything, so the pager would offer four pages of five rows when the
 * filter admitted one - a list that contradicts its own pager is worse than one that does not
 * filter at all. The term is only ever matched against what the row stores, the address and the
 * user agent; see `SessionSortField` for why the geo fields are not offered.
 */
export const getSessionHistory = ({
  page,
  pageSize,
  search,
  sortBy = 'visitedAt',
  sortOrder = 'desc',
  signal,
}: {
  page: number
  pageSize: number
  search?: string
  sortBy?: SessionSortField
  sortOrder?: SessionSortOrder
  signal?: AbortSignal
}): Promise<SessionHistoryResponse> => {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })

  // Built as parameters rather than interpolated into the path: a space, a `+` or an `&` in
  // what somebody typed looking for a user agent would otherwise change the meaning of the
  // request instead of being part of the term.
  if (search) query.set('search', search)

  query.set('sortBy', sortBy)
  query.set('sortOrder', sortOrder)

  return apiGet<SessionHistoryResponse>(`/auth/sessions?${query.toString()}`, signal)
}

/**
 * One page of **the installation's** visit history: `GET /admin/sessions`.
 *
 * The twin of `getSessionHistory` in every parameter, and the reason is the same: `total`
 * makes paging, filtering and ordering a server-side job rather than a browser-side one.
 * What differs is whose rows come back - the caller's own by the token there, and here the
 * rows of every account the caller's group may read, narrowed per target role by
 * `viewClients` / `viewResellers` / `viewAdmins`.
 *
 * `userId` is the per-account filter and lives in the address (`?userId=`) for the reason
 * the accounts table's `groupId` does: it is what a "show this account" link would carry,
 * and a filter kept in component state would be gone by the time the page mounted. `null`
 * means "every account I may read" and is left out of the query entirely - a cleared filter
 * has no business appearing in it.
 *
 * No antiforgery token, like every GET here: the call changes nothing, so there is no state
 * for a cross-site submission to alter.
 */
export const getAdminSessionHistory = ({
  page,
  pageSize,
  search,
  userId,
  sortBy = 'visitedAt',
  sortOrder = 'desc',
  signal,
}: {
  page: number
  pageSize: number
  search?: string
  userId?: string | null
  sortBy?: SessionSortField
  sortOrder?: SessionSortOrder
  signal?: AbortSignal
}): Promise<AdminSessionHistoryResponse> => {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })

  // Built as parameters rather than interpolated into the path: a space, a `+` or an `&` in
  // what somebody typed looking for a user agent would otherwise change the meaning of the
  // request instead of being part of the term.
  if (search) query.set('search', search)
  if (userId) query.set('userId', userId)

  query.set('sortBy', sortBy)
  query.set('sortOrder', sortOrder)

  return apiGet<AdminSessionHistoryResponse>(`/admin/sessions?${query.toString()}`, signal)
}

/**
 * Ends one session of an account the caller may read, so that no refresh token in its chain
 * can be exchanged again.
 *
 * The id is the **session**, not the row: several rows of one sign-in share it, because
 * ending a session is an act on the chain rather than on the rotation that happened to be
 * on screen. That is also why the page re-reads the list after this answers - one revoke
 * turns every row of that session on every page into an ended one, and deciding in the
 * browser which those are would be deciding what the server already knows.
 *
 * The endpoint answers `400 cannot_revoke_current` for the session the request arrived
 * with and `404 session_not_found` for anything else it will not end - unknown, already
 * ended, quietly expired and belonging to an account outside the caller's grants are one
 * answer on purpose, so the id cannot be used to probe which accounts hold a signed-in
 * session. The antiforgery token is minted at call time, never cached: a token bound to an
 * earlier identity is exactly what the server refuses with `csrf_invalid`.
 */
export const revokeAdminSession = ({
  sessionId,
  csrfToken,
}: {
  sessionId: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/sessions/${encodeURIComponent(sessionId)}`, {
    csrfToken,
    method: 'DELETE',
  })

/**
 * The answer to "end all sessions but this one": the code every answer carries, plus how many
 * sessions it ended.
 *
 * Its own interface rather than `MessageResponse` because the count is the whole reason the
 * endpoint exists - the button on the page says "N sessions will be signed out" before it is
 * pressed and the server's own number is what confirms it. Zero is a success, not a failure:
 * the account held only this one session, and the page says so in its own sentence.
 */
export interface RevokeOtherSessionsResult extends MessageResponse {
  /** How many sessions were ended. */
  revokedCount: number
}

/**
 * Every session this account currently holds, newest first.
 *
 * A plain GET behind the session, like the visit history: no antiforgery token (nothing is
 * changed), and the account comes from the token rather than from a parameter, so a caller
 * reads only its own list. The rows are sessions rather than tokens - a rotation does not add
 * a row, it moves the one that exists.
 */
export const getActiveSessions = (signal?: AbortSignal): Promise<ActiveSessionList> =>
  apiGet<ActiveSessionList>('/auth/active-sessions', signal)

/**
 * Ends one session of this account, so no refresh token in its chain can be exchanged again.
 *
 * The endpoint refuses the session the request arrived with (`cannot_revoke_current`) and
 * answers 404 for an id this account does not hold - unknown, gone and belonging to somebody
 * else are one answer on purpose, so the id cannot be used to probe for other accounts'
 * sessions. The antiforgery token is minted at call time, never cached: a token bound to an
 * earlier identity is exactly what the server refuses with `csrf_invalid`.
 */
export const revokeActiveSession = ({
  sessionId,
  csrfToken,
}: {
  sessionId: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/auth/active-sessions/${encodeURIComponent(sessionId)}`, {
    csrfToken,
    method: 'DELETE',
  })

/**
 * Ends every session of this account except the one this browser holds.
 *
 * No body and no id on purpose: the spared session comes from the token's own session claim,
 * so a caller cannot name "which session to keep" and end the one the request is
 * authenticated with. The id travels in the path of neither call for the same reason.
 */
export const revokeOtherActiveSessions = ({
  csrfToken,
}: {
  csrfToken: string | null
}): Promise<RevokeOtherSessionsResult> =>
  apiFetch<RevokeOtherSessionsResult>('/auth/active-sessions/others', {
    csrfToken,
    method: 'DELETE',
  })
