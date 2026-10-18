import { apiFetch, apiGet } from '../../utils/http'
import type {
  BulkOperationResponse,
  LoginGuardSettings,
  MessageResponse,
  UserStatus,
} from '../../types'
import type { AdminUserDetail, AdminUserList } from './types'

/**
 * The accounts table: reading a page and a row, creating, changing, confirming, blocking and
 * deleting an account, and one request that runs a single operation over a selection.
 *
 * Everything here is behind the admin session and a `viewUsers`/`editUsers` grant rather
 * than a captcha: the session is the stronger gate, which is why no call in this file carries
 * a captcha token.
 */

/**
 * Which column the admin's user table is ordered by.
 *
 * Seven exist because the server can order by seven: `username` (the default), `email`,
 * `group`, `role`, `status`, `lastSeenAt` and `ip`. The last two are correlated subqueries
 * over the freshest refresh token rather than columns of the row, and a value no server can
 * sort on would be a header that flips its arrow and changes nothing - so this list is the
 * endpoint's, not the page's idea of what is sortable.
 *
 * `group` and `role` are both here and neither replaces the other. The first is an order on
 * names the table draws (`Clients`, `Resellers`), the second is an order on levels
 * (Client &lt; Reseller &lt; Admin) - "all the administrators first" is a different question
 * from "groups in alphabetical order" and cannot be derived from it, so a caller who wants
 * either says which.
 */
export type AdminUserSortField =
  | 'username'
  | 'email'
  | 'group'
  | 'role'
  | 'status'
  | 'lastSeenAt'
  | 'ip'

/** Which way a page of accounts runs. `asc` by default - the list starts at A. */
export type AdminUserSortOrder = 'asc' | 'desc'

/**
 * One page of accounts, asked for in full - the account picker behind the visit history's
 * `?userId=` filter uses this, for the same reason the group picker uses
 * `ALL_GROUPS_PAGE_SIZE`: one route is one thing to keep correct, and the count a picker
 * would ignore is the same number the table needs.
 *
 * It is a page size rather than a promise, and the difference matters more here than it
 * does for groups. Groups are few by construction - three an installation cannot operate
 * without plus whatever an operator adds - while accounts are one row per person, so the
 * first hundred is a real ceiling to run into. Past it the picker offers what it was given
 * and the visitor narrows the table by the search beside it instead, which is the honest
 * degradation: an account the picker has never seen can still be reached by typing its
 * address into the history's own search box, which matches rows rather than names.
 *
 * The ceiling is the server's own (`AdminUserListLimits.MaxPageSize`) - asking for more
 * comes back `400 validation_failed` on `pageSize` rather than with a shorter page, so a
 * constant here that drifted from the endpoint would break the picker rather than silently
 * shrink it.
 */
export const ALL_USERS_PAGE_SIZE = 100

/**
 * One page of accounts, ordered and filtered by the server: `GET /admin/users`.
 *
 * A plain GET behind the admin session, so no antiforgery token - nothing is changed, and
 * the token's job is to prove that a write was aimed at this site. `page`, `pageSize`,
 * `search`, `sortBy` and `sortOrder` are built with `URLSearchParams` for the reason the
 * visit history builds its own the same way: a `+`, an `&` or a space in what somebody
 * typed looking for a username would otherwise change the meaning of the request instead of
 * being part of the term.
 *
 * `groupId` and `status` are `null` for "no filter" rather than an empty string, because
 * the server distinguishes neither from an absent parameter and a filter the page has
 * cleared has no business appearing in the query at all. `groupId` is an identifier rather
 * than an enum member name for the reason the account picker sends one: the level is the
 * group's, so the only thing that can name "the accounts of *that* group" is the group's
 * own id - a level filter would return every group of that level at once, which is a
 * different and much broader question. `status`, when present, is still the name of the enum
 * member - `Blocked` - which is the spelling every contract on both sides uses.
 *
 * `total` comes back with the rows and is counted under the same filters, before the page
 * is cut: a pager computed in the browser over one page would offer four pages of five rows
 * when the filter admitted one.
 */
export const getUsers = ({
  page,
  pageSize,
  search,
  groupId,
  status,
  sortBy,
  sortOrder,
  signal,
}: {
  page: number
  pageSize: number
  search?: string
  /** Group to keep, or null to keep every group. */
  groupId?: string | null
  status?: UserStatus | null
  sortBy?: AdminUserSortField
  sortOrder?: AdminUserSortOrder
  signal?: AbortSignal
}): Promise<AdminUserList> => {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })

  if (search) query.set('search', search)
  if (groupId) query.set('groupId', groupId)
  if (status) query.set('status', status)
  query.set('sortBy', sortBy ?? 'username')
  query.set('sortOrder', sortOrder ?? 'asc')

  return apiGet<AdminUserList>(`/admin/users?${query.toString()}`, signal)
}

/**
 * One account in full, for the edit form: `GET /admin/users/{id}`.
 *
 * A plain GET like the list. The identifier travels in the path rather than the body, and a
 * row this administrator cannot see - or one that does not exist - is `404 user_not_found`
 * either way, so the endpoint is no probe for accounts outside its reach.
 */
export const getUser = (id: string, signal?: AbortSignal): Promise<AdminUserDetail> =>
  apiGet<AdminUserDetail>(`/admin/users/${encodeURIComponent(id)}`, signal)

/**
 * What the add form sends to make an account in one request.
 *
 * Every field the form holds, and none it does not: there is no id (the server mints it)
 * and no password confirmation (the form collects one password, so a second field would
 * only be able to disagree with the first). `password` travels in clear text exactly once,
 * from here to the service that hashes it, and is never part of a response.
 *
 * `groupId` and `status` are required values rather than defaults on this side: an account
 * with no group is one every role check has to guess about - the level *is* the group's -
 * and `Unregistered` and `Registered` are not the same account. The form always knows both,
 * so neither is ever left for the server to invent - a default is a decision about somebody
 * else's account.
 *
 * There is deliberately no `role` here. The level is not a field of the account at all: it
 * is read back out of whichever group `groupId` names, so a body claiming both would be a
 * body that could contradict itself, and the server would have to decide which of the two
 * to believe.
 */
export interface NewUserValues {
  username: string
  email: string
  password: string
  /** Group the account joins. Its level, and therefore the area it may reach, follows from this. */
  groupId: string
  status: UserStatus
  /** IANA identifier, or null when the browser should decide for this account. */
  timeZone: string | null
  /** The two switches and the three allow lists, sent whole. */
  loginGuard: LoginGuardSettings
}

/**
 * Creates an account: `POST /admin/users`, answered with `201 user_created`.
 *
 * A write, so the antiforgery token is minted at call time and never cached - a token
 * bound to an earlier identity is exactly what the server refuses with `csrf_invalid`.
 * There is no captcha on this endpoint on purpose: the caller already holds an `AdminOnly`
 * session, which is a stronger gate than a score from reCAPTCHA, and the server does not
 * ask for a token here (see the backend's admin users section for the full reasoning).
 *
 * A taken username or email is `409 user_already_exists` with the offending field named in
 * `errors`, and the form shows it under that input rather than as a toast.
 */
export const createUser = ({
  values,
  csrfToken,
}: {
  values: NewUserValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/users', {
    method: 'POST',
    body: {
      username: values.username,
      email: values.email,
      password: values.password,
      groupId: values.groupId,
      status: values.status,
      timeZone: values.timeZone,
      loginGuard: values.loginGuard,
    },
    csrfToken,
  })

/**
 * What the edit form may change: every property is optional and an absent one means "leave
 * it alone" - which is what stops a request from clearing a value simply because it forgot
 * to include it.
 *
 * Two need their own rule because JSON cannot say "absent" and "null" apart once the body
 * has been bound, and the form states both explicitly rather than relying on omission:
 * `password` is `null` (or empty, at the server) for "keep the current one" - the field
 * starts blank and is only sent when something was typed into it - and `timeZone` is `null`
 * for "keep" while an empty string means "clear it" and go back to following the browser.
 * The form therefore always sends the string it holds: `''` for "automatic".
 */
export interface UserPatch {
  username?: string
  email?: string
  password?: string | null
  /** New group. Moving an account between groups moves its level with it - that is the point of both. */
  groupId?: string
  status?: UserStatus
  timeZone?: string | null
  /** Replacement guard, sent whole when sent at all - a half a caller never speaks about would be a guard nobody can read. */
  loginGuard?: LoginGuardSettings
}

/**
 * Changes an account: `PATCH /admin/users/{id}`, answered with `200 user_updated`.
 *
 * The same rules as creation on every field they share, and the same 409 with the field
 * named when a username or address is taken by another account. `status` travels as an enum
 * member name like everything else, `groupId` as the identifier of a group that exists - and
 * a status change is a state transition the server owns: `RegisteredAt` is stamped and
 * cleared by it, sessions are revoked by it, and none of that is something the page could do
 * correctly on its own. Changing the group is the same kind of move for the same reason: the
 * level, the effective status and every permission the account may use are all read out of
 * wherever it lands.
 */
export const updateUser = ({
  id,
  patch,
  csrfToken,
}: {
  id: string
  patch: UserPatch
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/users/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: patch,
    csrfToken,
  })

/**
 * The three state transitions a row offers, as one helper behind three named calls.
 *
 * Each is a `POST` to an addressed action rather than a `PATCH` of `status`, because the
 * difference matters to the answer: confirming a registration is not the same move as
 * writing the value `Registered` (it refuses an account that is not waiting for one with
 * `409 invalid_status`), and each action carries its own success code - `registration_confirmed`,
 * `user_blocked`, `user_unblocked` - which a single `user_updated` would have erased.
 */
const transitionUser = (
  id: string,
  action: 'confirm-registration' | 'block' | 'unblock',
  csrfToken: string | null,
): Promise<MessageResponse> =>
  apiFetch(`/admin/users/${encodeURIComponent(id)}/${action}`, {
    method: 'POST',
    csrfToken,
  })

/** Confirms an account still waiting for its email code: `200 registration_confirmed`, `409 invalid_status` when nothing is waiting. */
export const confirmUserRegistration = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> => transitionUser(id, 'confirm-registration', csrfToken)

/** Blocks an account: `200 user_blocked`, and every session it holds ends on its next refresh. Idempotent by design - blocking twice is still blocked. */
export const setUserBlocked = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> => transitionUser(id, 'block', csrfToken)

/** Unblocks an account: `200 user_unblocked`, `409 invalid_status` when it is not blocked. The state it returns to is the server's to decide - registered, or waiting, depending on `registeredAt`. */
export const setUserUnblocked = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> => transitionUser(id, 'unblock', csrfToken)

/**
 * Deletes an account: `DELETE /admin/users/{id}`, answered with `200 user_deleted`.
 *
 * The only action on this page that asks first, because it is the only one that cannot be
 * undone: everything else moves a row between states, this removes the row and takes its
 * sessions, its login-guard rules and its pending changes with it. The caller's own account
 * is refused with `400 cannot_delete_self` rather than by hiding a button - the endpoint
 * answers for itself, and the disabled control is only the UI saying the same thing.
 */
export const deleteUser = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/users/${encodeURIComponent(id)}`, {
    method: 'DELETE',
    csrfToken,
  })

/**
 * The five operations one request can run over a set of accounts, as the text the server
 * parses them from.
 *
 * The hyphens are the server's own spelling rather than a local invention: `ParseBulkAction`
 * compares the trimmed, lower-cased text against exactly these five, and a name that maps to
 * none of them is refused with `validation_failed` listing the ones that do. A union rather
 * than `string` so that a sixth name is a compile error here instead of a 400 from an
 * endpoint the caller has already reached.
 */
export type BulkUserAction =
  | 'block'
  | 'unblock'
  | 'delete'
  | 'confirm-registration'
  | 'assign-group'

/**
 * Runs one operation over a set of accounts: `POST /admin/users/bulk`.
 *
 * The identifiers travel explicitly rather than as a filter, because the selection a table
 * offers is the rows of the page the operator is looking at - the request names them instead
 * of describing a predicate whose result nobody has seen. They are bounded at one page on the
 * server (`BulkOperationLimits.MaxItems`, 100), which is also the ceiling the page size
 * picker offers, so a selection this page can make is always a selection the endpoint accepts.
 *
 * **The answer is `200` for the run, not for every row.** A well-formed bulk request is not a
 * partial failure: every identifier came back with a verdict, and each one sits in `items`
 * carrying the same code the single-row endpoint would have given it. The caller reports
 * those verdicts rather than reading one status as a verdict for all of them.
 *
 * `groupId` belongs to `assign-group` alone. It travels as `null` for the other four rather
 * than being dropped from the body, so there is one request shape to read rather than one
 * that quietly changes meaning between five operations.
 */
export const bulkUsers = ({
  ids,
  action,
  groupId = null,
  csrfToken,
}: {
  ids: string[]
  action: BulkUserAction
  groupId?: string | null
  csrfToken: string | null
}): Promise<BulkOperationResponse> =>
  apiFetch('/admin/users/bulk', {
    method: 'POST',
    body: { action, ids, groupId },
    csrfToken,
  })
