import { apiFetch, apiGet } from './http'
import { getCsrfToken } from './csrf'
import type {
  ActiveSessionList,
  AdminUserDetail,
  AdminUserList,
  GeoLookup,
  LoginGuardSettings,
  MessageResponse,
  PasswordStatusResponse,
  ProfileResponse,
  Role,
  Session,
  SessionHistoryResponse,
  UserStatus,
} from '../types'

/**
 * The two fields every sign-in form submits, and the three the registration and public
 * reset forms do.
 *
 * They are spelled out here rather than typed `any` or taken from antd's `Form` so that
 * the endpoints do not depend on how the forms are built: `submitAuth` cares what the
 * server is sent, not which `Form.Item` the value was read from. Exported because the
 * forms name them too - `AuthForm` is generic in what its page collects, and the type
 * argument at each call site is one of these.
 */
export interface SignInValues {
  username: string
  password: string
}

export interface RegistrationValues extends SignInValues {
  email: string
}

/**
 * What every call carries beside its values: the antiforgery token, and outside
 * development the captcha one.
 *
 * Both are `string | null` rather than `string` because that is what minting them answers
 * when it cannot - `getCsrfToken` is silent when the backend is absent, `getCaptchaToken`
 * is silent in development and whenever no site key is configured. Required rather than
 * optional because every call site already supplies them: a forgotten `csrfToken` compiles
 * just as happily as a wrong one and fails later, at the server, as `csrf_invalid`.
 */
export interface CallOptions {
  csrfToken: string | null
  captchaToken: string | null
}

/**
 * The three sign-in endpoints, as kebab-case - `client-login`, `reseller-login`,
 * `admin-login` - which is also the path under `/auth/`.
 *
 * A union rather than `string` because there are exactly three doors and each has its own
 * attempt window behind it; an endpoint that does not exist is a typo that would otherwise
 * arrive as a 404 the form has no text for.
 */
type LoginEndpoint = 'client-login' | 'reseller-login' | 'admin-login'

/**
 * Actions a reCAPTCHA token has to have been minted for. The backend fixes one per
 * endpoint rather than letting the caller choose, so these are half of a contract and a
 * value here has to match the matching constant on the matching controller.
 */
export const RegisterCaptchaAction = 'register'
export const ConfirmCaptchaAction = 'register_confirm'
export const ClientLoginCaptchaAction = 'client_login'
export const ResellerLoginCaptchaAction = 'reseller_login'
export const AdminLoginCaptchaAction = 'admin_login'

/**
 * The actions of the two flows added after registration: changing the profile of an
 * account that is signed in, and resetting a password from the public form.
 *
 * The sign-in actions are split per role because each entrance is a different door with a
 * different attempt window behind it. These are not: a profile change has no role to speak
 * of (any signed-in account may change its own), and neither has a reset, which is reached
 * by somebody who by definition has no session yet. One action per endpoint rather than one
 * per caller, which is the rule the whole table follows.
 */
export const ProfileChangeCaptchaAction = 'profile_change'
export const ProfileConfirmCaptchaAction = 'profile_confirm'
export const PasswordResetCaptchaAction = 'password_reset'
export const PasswordResetConfirmCaptchaAction = 'password_reset_confirm'

/**
 * Asks for a registration.
 *
 * The captcha action and token are deliberately not part of the body. The server reads
 * the token from the `X-Recaptcha-Token` header and ignores both body fields, so sending
 * them would suggest that the client chooses the action - it does not.
 */
export const submitRegistration = ({
  values,
  csrfToken,
  captchaToken,
}: CallOptions & { values: RegistrationValues }): Promise<MessageResponse> =>
  apiFetch('/auth/registrations', {
    body: { username: values.username, email: values.email, password: values.password },
    csrfToken,
    captchaToken,
  })

/** Answers with the code that was mailed to `email`, and with a session on success. */
export const confirmRegistration = ({
  email,
  code,
  csrfToken,
  captchaToken,
}: CallOptions & { email: string; code: string }): Promise<MessageResponse> =>
  apiFetch('/auth/registrations/confirm', {
    body: { email, code },
    csrfToken,
    captchaToken,
  })

/**
 * Submits one of the login forms.
 *
 * `action` is the kebab-case endpoint name - `client-login`, `reseller-login`,
 * `admin-login` - which is also the captcha action with the underscore swapped for a
 * hyphen. They are kept as one value rather than two because they are the same fact about
 * the same form written twice, and two constants that have to agree will eventually stop
 * agreeing.
 */
export const submitAuth = ({
  action,
  values,
  csrfToken,
  captchaToken,
}: CallOptions & { action: LoginEndpoint; values: SignInValues }): Promise<MessageResponse> =>
  apiFetch(`/auth/${action}`, {
    body: { username: values.username, password: values.password },
    csrfToken,
    captchaToken,
  })

/**
 * Ends the session and clears the cookies.
 *
 * The tokens are in `HttpOnly` cookies, so this is the only way out of a signed-in state:
 * there is nothing on the page to delete. The server revokes the refresh chain and blocks
 * the access token it was holding, so the session is over rather than merely forgotten -
 * without that, a sign-out on a shared machine would keep working for whoever came next.
 *
 * The endpoint belongs to signed-in visitors: a caller with no session is refused with
 * `auth_required` rather than being told a session was signed out, and an access token that
 * has merely expired is refreshed by `http.ts` on the way, so the refusal means there was
 * genuinely nothing to end.
 */
export const signOut = async ({
  csrfToken,
}: { csrfToken?: string | null } = {}): Promise<MessageResponse> => {
  const token = csrfToken ?? (await getCsrfToken())
  return apiFetch('/auth/logout', { csrfToken: token })
}

/**
 * Who the browser is already signed in as.
 *
 * Needed because the tokens are `HttpOnly`: the page cannot read its own cookie, so it has
 * to ask. Returns `null` rather than throwing when there is no session, since "not signed
 * in" is the answer to the question on a public page and not a failure.
 */
export const currentSession = async (): Promise<Session | null> => {
  try {
    return await apiGet<Session>('/auth/me')
  } catch {
    return null
  }
}

/**
 * The three facts the profile page shows: username, email and role.
 *
 * It is not `currentSession`. That one reports the claims of the token, and the address is
 * deliberately not among them - a claim is a copy that outlives the change it describes -
 * so anything that has to show or verify the current address has to ask the row.
 */
export const getProfile = (): Promise<ProfileResponse> => apiGet<ProfileResponse>('/auth/profile')

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
}: {
  page: number
  pageSize: number
  search?: string
  sortBy?: SessionSortField
  sortOrder?: SessionSortOrder
}): Promise<SessionHistoryResponse> => {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })

  // Built as parameters rather than interpolated into the path: a space, a `+` or an `&` in
  // what somebody typed looking for a user agent would otherwise change the meaning of the
  // request instead of being part of the term.
  if (search) query.set('search', search)

  query.set('sortBy', sortBy)
  query.set('sortOrder', sortOrder)

  return apiGet<SessionHistoryResponse>(`/auth/sessions?${query.toString()}`)
}

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
export const getActiveSessions = (): Promise<ActiveSessionList> =>
  apiGet<ActiveSessionList>('/auth/active-sessions')

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

/**
 * The one endpoint every profile edit goes through: `PATCH /auth/profile`.
 *
 * The backend collapsed the five `POST /auth/profile/*` paths into this single partial
 * update (REST: a profile is one resource, so it is edited by one method on one path).
 * The body names exactly one change — `username`, `email`, `newPassword`, `loginGuard`
 * or `timeZone` — and the server refuses a body naming none or two rather than picking
 * an order, because the staged-confirmation flow holds one pending change at a time.
 *
 * `body` is a record rather than a typed value shape because the five wrappers below
 * each build their own single-change body; the shared parts (the method, the path, the
 * two tokens) are what this helper exists to keep in one place.
 */
const patchProfile = ({
  body,
  csrfToken,
  captchaToken,
}: CallOptions & { body: Record<string, unknown> }): Promise<MessageResponse> =>
  apiFetch('/auth/profile', { method: 'PATCH', body, csrfToken, captchaToken })

/**
 * Starts changing one of the four sensitive fields, and answers with the outcome.
 *
 * A 2xx means one of two things and the `code` says which: `profile_updated` when this
 * installation has no mail server and the change is already on the account, and
 * `profile_change_submitted` when a code is on its way and the caller has to run it through
 * `confirmProfileChange`. Both are successes; only the second one continues.
 */
export const changeUsername = ({
  currentPassword,
  username,
  csrfToken,
  captchaToken,
}: CallOptions & { currentPassword: string; username: string }): Promise<MessageResponse> =>
  patchProfile({ body: { currentPassword, username }, csrfToken, captchaToken })

/** The code for an address change is mailed to the new one, never to the old one. */
export const changeEmail = ({
  currentPassword,
  email,
  csrfToken,
  captchaToken,
}: CallOptions & { currentPassword: string; email: string }): Promise<MessageResponse> =>
  patchProfile({ body: { currentPassword, email }, csrfToken, captchaToken })

/**
 * Starts changing the password. The new one replaces the old only once the mailed code is
 * entered, and doing so ends every session of the account - the browser that made the
 * change is signed straight back in, every other device has to sign in again.
 */
export const changePassword = ({
  currentPassword,
  newPassword,
  csrfToken,
  captchaToken,
}: CallOptions & { currentPassword: string; newPassword: string }): Promise<MessageResponse> =>
  patchProfile({ body: { currentPassword, newPassword }, csrfToken, captchaToken })

/**
 * Applies a pending profile change.
 *
 * No identifier travels with the code: the account comes from the session, so a code is
 * never a credential on its own and cannot be spent by whoever is holding it.
 */
export const confirmProfileChange = ({
  code,
  csrfToken,
  captchaToken,
}: CallOptions & { code: string }): Promise<MessageResponse> =>
  apiFetch('/auth/profile/confirm', {
    body: { code },
    csrfToken,
    captchaToken,
  })

/**
 * What the login guard form submits: the current password proving the session is the
 * owner's, plus the guard itself.
 *
 * The guard is `LoginGuardSettings` rather than a second spelling of the same six
 * fields, because the card edits that shape and the endpoint accepts it - one shape
 * with two spellings would drift the way every duplicated contract eventually does.
 */
export interface LoginGuardValues {
  currentPassword: string
  guard: LoginGuardSettings
}

/**
 * Starts changing the login guard: the two switches and the three allow lists.
 *
 * Like the other three changes, a 2xx is one of two things and the `code` says which:
 * `profile_updated` when the change is already on the account, and
 * `profile_change_submitted` when a code is on its way through `confirmProfileChange`.
 * A new guard touches no session either way - the refresh path rechecks each session
 * against the new lists on its next rotation.
 */
export const changeLoginGuard = ({
  currentPassword,
  guard,
  csrfToken,
  captchaToken,
}: CallOptions & { currentPassword: string; guard: LoginGuardSettings }): Promise<MessageResponse> =>
  patchProfile({
    body: {
      currentPassword,
      loginGuard: {
        geoProtectionEnabled: guard.geoProtectionEnabled,
        bindSessionToIp: guard.bindSessionToIp,
        allowedIps: guard.allowedIps,
        allowedCountry: guard.allowedCountry,
        allowedAutonomousSystemNumber: guard.allowedAutonomousSystemNumber,
      },
    },
    csrfToken,
    captchaToken,
  })

/**
 * What the GeoIP database knows about the network this browser arrived from.
 *
 * Exists for the "use my current network" button: a visitor cannot be expected to know
 * their own autonomous system number. Throws like every other call when the backend is
 * unreachable - the button then toasts the transport error rather than filling the form
 * with nothing.
 */
export const lookupGeo = (): Promise<GeoLookup> => apiGet<GeoLookup>('/auth/geo/lookup')

/**
 * Sets the time zone the profile is displayed in, or clears it with `null` so the browser's
 * own decides again.
 *
 * The one profile call without a captcha token: the endpoint expects no captcha (a
 * signed-in visitor saving a display preference), and a header the server does not read
 * would only suggest otherwise. The antiforgery token is still required - it is checked on
 * every write of this API, session or not.
 *
 * `null` is sent as an empty string, and that is the contract rather than a detail: on the
 * merged `PATCH` an absent `timeZone` means "leave it alone" while an explicit empty string
 * means "clear the preference", so a literal `null` in the body would be read as no change
 * at all and the button would appear to do nothing.
 */
export const updateTimezone = ({
  timeZone,
  csrfToken,
}: {
  timeZone: string | null
  csrfToken: string | null
}): Promise<MessageResponse> =>
  patchProfile({ body: { timeZone: timeZone ?? '' }, csrfToken, captchaToken: null })

/**
 * Whether this installation can send a code at all.
 *
 * Read before the reset form is rendered, so a visitor never types into a form whose second
 * step cannot exist on this server. It answers `false` rather than throwing when the
 * question cannot be asked - the refusal is then the same one the request itself would get.
 */
export const passwordResetStatus = async (): Promise<boolean> => {
  try {
    const result = await apiGet<PasswordStatusResponse>('/auth/password-resets/status')
    return result?.configured === true
  } catch {
    return false
  }
}

/**
 * Checks the username and email pair, stores the requested password and mails the code.
 *
 * `password` in the body rather than `newPassword`, because the field on the form is
 * `password` - it is the registration form reused, and a rejection reported under a name
 * the form has no input for would arrive as a bare toast instead of a reason under the
 * input.
 */
export const requestPasswordReset = ({
  values,
  csrfToken,
  captchaToken,
}: CallOptions & { values: RegistrationValues }): Promise<MessageResponse> =>
  apiFetch('/auth/password-resets', {
    body: { username: values.username, email: values.email, password: values.password },
    csrfToken,
    captchaToken,
  })

/**
 * Applies the password whose code was mailed out.
 *
 * The login name travels with the code because a code is stored as a hash and cannot be
 * looked up backwards: the name picks the pending row, the code decides whether it opens.
 */
export const confirmPasswordReset = ({
  username,
  code,
  csrfToken,
  captchaToken,
}: CallOptions & { username: string; code: string }): Promise<MessageResponse> =>
  apiFetch('/auth/password-resets/confirm', {
    body: { username, code },
    csrfToken,
    captchaToken,
  })

/**
 * Which column the admin's user table is ordered by.
 *
 * Six exist because the server can order by six: `username` (the default), `email`, `role`,
 * `status`, `lastSeenAt` and `ip`. The last two are correlated subqueries over the freshest
 * refresh token rather than columns of the row, and a value no server can sort on would be a
 * header that flips its arrow and changes nothing - so this list is the endpoint's, not the
 * page's idea of what is sortable.
 */
export type AdminUserSortField = 'username' | 'email' | 'role' | 'status' | 'lastSeenAt' | 'ip'

/** Which way a page of accounts runs. `asc` by default - the list starts at A. */
export type AdminUserSortOrder = 'asc' | 'desc'

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
 * `role` and `status` are `null` for "no filter" rather than an empty string, because the
 * server distinguishes neither from an absent parameter and a filter the page has cleared
 * has no business appearing in the query at all. When present they are the name of the enum
 * member - `Admin`, `Blocked` - which is the spelling every contract on both sides uses.
 *
 * `total` comes back with the rows and is counted under the same filters, before the page
 * is cut: a pager computed in the browser over one page would offer four pages of five rows
 * when the filter admitted one.
 */
export const getUsers = ({
  page,
  pageSize,
  search,
  role,
  status,
  sortBy,
  sortOrder,
}: {
  page: number
  pageSize: number
  search?: string
  role?: Role | null
  status?: UserStatus | null
  sortBy?: AdminUserSortField
  sortOrder?: AdminUserSortOrder
}): Promise<AdminUserList> => {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })

  if (search) query.set('search', search)
  if (role) query.set('role', role)
  if (status) query.set('status', status)
  query.set('sortBy', sortBy ?? 'username')
  query.set('sortOrder', sortOrder ?? 'asc')

  return apiGet<AdminUserList>(`/admin/users?${query.toString()}`)
}

/**
 * One account in full, for the edit form: `GET /admin/users/{id}`.
 *
 * A plain GET like the list. The identifier travels in the path rather than the body, and a
 * row this administrator cannot see - or one that does not exist - is `404 user_not_found`
 * either way, so the endpoint is no probe for accounts outside its reach.
 */
export const getUser = (id: string): Promise<AdminUserDetail> =>
  apiGet<AdminUserDetail>(`/admin/users/${encodeURIComponent(id)}`)

/**
 * What the add form sends to make an account in one request.
 *
 * Every field the form holds, and none it does not: there is no id (the server mints it)
 * and no password confirmation (the form collects one password, so a second field would
 * only be able to disagree with the first). `password` travels in clear text exactly once,
 * from here to the service that hashes it, and is never part of a response.
 *
 * `role` and `status` are required values rather than defaults on this side: an account
 * with no level is one every role check has to guess about, and `Unregistered` and
 * `Registered` are not the same account. The form always knows both, so neither is ever
 * left for the server to invent - a default is a decision about somebody else's account.
 */
export interface NewUserValues {
  username: string
  email: string
  password: string
  role: Role
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
      role: values.role,
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
  role?: Role
  status?: UserStatus
  timeZone?: string | null
  /** Replacement guard, sent whole when sent at all - a half a caller never speaks about would be a guard nobody can read. */
  loginGuard?: LoginGuardSettings
}

/**
 * Changes an account: `PATCH /admin/users/{id}`, answered with `200 user_updated`.
 *
 * The same rules as creation on every field they share, and the same 409 with the field
 * named when a username or address is taken by another account. `role` and `status` travel
 * as enum member names like everything else, and a status change is a state transition the
 * server owns - `RegisteredAt` is stamped and cleared by it, sessions are revoked by it,
 * and none of that is something the page could do correctly on its own.
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