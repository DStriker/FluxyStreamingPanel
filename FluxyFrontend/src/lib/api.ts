import { apiFetch, apiGet } from './http'
import { getCsrfToken } from './csrf'
import type {
  GeoLookup,
  LoginGuardSettings,
  MessageResponse,
  PasswordStatusResponse,
  ProfileResponse,
  Session,
  SessionHistoryResponse,
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
  apiFetch('/auth/register', {
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
  apiFetch('/auth/register/confirm', {
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
 * Starts changing one of the three, and answers with the outcome.
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
  apiFetch('/auth/profile/username', {
    body: { currentPassword, username },
    csrfToken,
    captchaToken,
  })

/** The code for an address change is mailed to the new one, never to the old one. */
export const changeEmail = ({
  currentPassword,
  email,
  csrfToken,
  captchaToken,
}: CallOptions & { currentPassword: string; email: string }): Promise<MessageResponse> =>
  apiFetch('/auth/profile/email', {
    body: { currentPassword, email },
    csrfToken,
    captchaToken,
  })

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
  apiFetch('/auth/profile/password', {
    body: { currentPassword, newPassword },
    csrfToken,
    captchaToken,
  })

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
  apiFetch('/auth/profile/geo', {
    body: {
      currentPassword,
      geoProtectionEnabled: guard.geoProtectionEnabled,
      bindSessionToIp: guard.bindSessionToIp,
      allowedIps: guard.allowedIps,
      allowedCountry: guard.allowedCountry,
      allowedAutonomousSystemNumber: guard.allowedAutonomousSystemNumber,
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
 */
export const updateTimezone = ({
  timeZone,
  csrfToken,
}: {
  timeZone: string | null
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/auth/profile/timezone', {
    body: { timeZone },
    csrfToken,
  })

/**
 * Whether this installation can send a code at all.
 *
 * Read before the reset form is rendered, so a visitor never types into a form whose second
 * step cannot exist on this server. It answers `false` rather than throwing when the
 * question cannot be asked - the refusal is then the same one the request itself would get.
 */
export const passwordResetStatus = async (): Promise<boolean> => {
  try {
    const result = await apiGet<PasswordStatusResponse>('/auth/password/status')
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
  apiFetch('/auth/password/forgot', {
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
  apiFetch('/auth/password/reset', {
    body: { username, code },
    csrfToken,
    captchaToken,
  })