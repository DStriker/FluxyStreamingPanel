import { apiFetch, apiGet } from '../../lib/http'
import { getCsrfToken } from '../../lib/csrf'
import type { MessageResponse, Session } from '../../types'
import type { PasswordStatusResponse } from './types/PasswordStatusResponse'
import type { CallOptions } from '../../lib/http'

/**
 * Registration, sign-in and the session itself: the endpoints that turn a visitor into a
 * signed-in account, and the one that reports who the browser already is. The public
 * password reset lives at the bottom of this file, because it is the same visitor, still
 * without a session.
 *
 * The captcha action names of these flows live here because they are one table on the
 * server; the profile flow names its own beside its endpoints, in `features/profile/api.ts`.
 */

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
 * The three sign-in endpoints, as kebab-case - `client-login`, `reseller-login`,
 * `admin-login` - which is also the path under `/auth/`.
 *
 * A union rather than `string` because there are exactly three doors and each has its own
 * attempt window behind it; an endpoint that does not exist is a typo that would otherwise
 * arrive as a 404 the form has no text for.
 */
type LoginEndpoint = 'client-login' | 'reseller-login' | 'admin-login'

/**
 * Actions a reCAPTCHA token has to have been minted for on the registration and sign-in
 * endpoints: the endpoints in this file that take one. The backend fixes one per endpoint
 * rather than letting the caller choose, so these are half of a contract and a value here
 * has to match the matching constant on the matching controller.
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
 * The public password reset: whether this installation can send a code at all, and the two
 * steps that spend one.
 *
 * Reached by somebody who by definition has no session yet, which is why - unlike the three
 * sign-ins - its two actions are not split per role.
 */

/**
 * The actions a reCAPTCHA token has to have been minted for on the two reset endpoints.
 */
export const PasswordResetCaptchaAction = 'password_reset'
export const PasswordResetConfirmCaptchaAction = 'password_reset_confirm'

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
