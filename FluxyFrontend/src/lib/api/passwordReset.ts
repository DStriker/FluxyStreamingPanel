import { apiFetch, apiGet } from '../http'
import type { MessageResponse, PasswordStatusResponse } from '../../types'
import type { CallOptions } from './common'
import type { RegistrationValues } from './auth'

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
