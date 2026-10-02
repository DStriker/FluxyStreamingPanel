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