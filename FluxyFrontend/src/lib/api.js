import { apiFetch, apiGet } from './http'
import { getCsrfToken } from './csrf'

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
export const submitRegistration = ({ values, csrfToken, captchaToken }) =>
  apiFetch('/auth/register', {
    body: { username: values.username, email: values.email, password: values.password },
    csrfToken,
    captchaToken,
  })

/** Answers with the code that was mailed to `email`, and with a session on success. */
export const confirmRegistration = ({ email, code, csrfToken, captchaToken }) =>
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
export const submitAuth = ({ action, values, csrfToken, captchaToken }) =>
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
 * has merely expired is refreshed by `http.js` on the way, so the refusal means there was
 * genuinely nothing to end.
 */
export const signOut = async ({ csrfToken } = {}) => {
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
export const currentSession = async () => {
  try {
    return await apiGet('/auth/me')
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
export const getProfile = () => apiGet('/auth/profile')

/**
 * Starts changing one of the three, and answers with the outcome.
 *
 * A 2xx means one of two things and the `code` says which: `profile_updated` when this
 * installation has no mail server and the change is already on the account, and
 * `profile_change_submitted` when a code is on its way and the caller has to run it through
 * `confirmProfileChange`. Both are successes; only the second one continues.
 */
export const changeUsername = ({ currentPassword, username, csrfToken, captchaToken }) =>
  apiFetch('/auth/profile/username', {
    body: { currentPassword, username },
    csrfToken,
    captchaToken,
  })

/** The code for an address change is mailed to the new one, never to the old one. */
export const changeEmail = ({ currentPassword, email, csrfToken, captchaToken }) =>
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
export const changePassword = ({ currentPassword, newPassword, csrfToken, captchaToken }) =>
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
export const confirmProfileChange = ({ code, csrfToken, captchaToken }) =>
  apiFetch('/auth/profile/confirm', {
    body: { code },
    csrfToken,
    captchaToken,
  })

/**
 * Whether this installation can send a code at all.
 *
 * Read before the reset form is rendered, so a visitor never types into a form whose second
 * step cannot exist on this server. It answers `false` rather than throwing when the
 * question cannot be asked - the refusal is then the same one the request itself would get.
 */
export const passwordResetStatus = async () => {
  try {
    const result = await apiGet('/auth/password/status')
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
export const requestPasswordReset = ({ values, csrfToken, captchaToken }) =>
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
export const confirmPasswordReset = ({ username, code, csrfToken, captchaToken }) =>
  apiFetch('/auth/password/reset', {
    body: { username, code },
    csrfToken,
    captchaToken,
  })