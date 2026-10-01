import { apiFetch } from './http'

/**
 * Actions a reCAPTCHA token has to have been minted for. The backend fixes one per
 * endpoint rather than letting the caller choose, so these are half of a contract and a
 * value here has to match the matching constant in `AuthController`.
 */
export const RegisterCaptchaAction = 'register'
export const ConfirmCaptchaAction = 'register_confirm'

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

/** Answers with the code that was mailed to `email`. */
export const confirmRegistration = ({ email, code, csrfToken, captchaToken }) =>
  apiFetch('/auth/register/confirm', {
    body: { email, code },
    csrfToken,
    captchaToken,
  })

/**
 * Submits one of the login forms.
 *
 * Kept as it was because the three endpoints it targets do not exist on the backend yet.
 * It is not a second, divergent transport - it goes through `apiFetch` like everything
 * else and will only need its URL filled in.
 */
export const submitAuth = ({ action, values, csrfToken, captchaToken }) =>
  apiFetch(`/auth/${action}`, {
    body: { username: values.username, password: values.password, ...(values.email ? { email: values.email } : {}) },
    csrfToken,
    captchaToken,
  })