import i18n from '../i18n'
import { csrfHeader } from './csrf'
import { apiUrl } from './url'

/**
 * Codes for the two failures that never reached the API, so a caller can branch on them
 * the same way it branches on a server code. The first is this process talking to nobody,
 * the second is a response that was not the documented body - a proxy error page, a
 * gateway 502, anything that is not the API.
 */
export const NetworkErrorCode = 'network_error'
export const ServerErrorCode = 'server_error'

/**
 * A failed call, carrying the machine readable `code` the server chose rather than only a
 * sentence.
 *
 * The code is what a caller should branch on, and it is what the localized text is looked
 * up by. `message` stays the server's English fallback for a code this build has no
 * translation for, which is why it is kept instead of being replaced at the throw site.
 */
export class ApiError extends Error {
  constructor({ code, message, status, errors = null }) {
    super(message)
    this.name = 'ApiError'
    this.code = code
    this.status = status
    /** Rejected fields as `{ fieldName: [reason, ...] }`, or null when nothing was rejected. */
    this.errors = errors
  }
}

/** Absolute URL of an API path, honouring the configured base. */
export { apiUrl }

/** Key in the locale files a server code is translated under, or null when there is none. */
const codeKey = (code) => (typeof code === 'string' && code ? `messages.api.${code}` : null)

/**
 * Localized text for a server `code`, or `null` when this build has no translation for it.
 * Callers decide what to show in that case rather than being handed a raw i18next key.
 */
export const textForCode = (code, options) => {
  const key = codeKey(code)
  return key && i18n.exists(key) ? i18n.t(key, options) : null
}

/**
 * Posts to the API and turns any non-2xx into an {@link ApiError}.
 *
 * Every call goes through here so that a rejection always has the same shape: a code to
 * branch on, text to show, and - when the server rejected specific fields - the map of
 * them keyed by the camelCase name of the JSON property, which is also the name the form
 * fields use.
 */
export async function apiFetch(path, { body, csrfToken, captchaToken } = {}) {
  let response

  try {
    response = await fetch(apiUrl(path), {
      method: 'POST',
      // The antiforgery cookies are the reason this is 'include' and not the default
      // 'same-origin' - the API is on another port, which is another origin to fetch.
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        // Sent only when there is one. In dev the captcha is skipped and produces no
        // token, and an empty header would only invite the question of whether it means
        // "no captcha" or "a broken captcha".
        ...(captchaToken ? { 'X-Recaptcha-Token': captchaToken } : {}),
        ...csrfHeader(csrfToken),
      },
      body: JSON.stringify(body ?? {}),
    })
  } catch {
    // No response at all: the request never arrived. Worth telling apart from a refusal,
    // because the fix is different - start the server rather than correct the input.
    throw new ApiError({
      code: NetworkErrorCode,
      message: i18n.t(`messages.api.${NetworkErrorCode}`),
      status: 0,
    })
  }

  const data = await response.json().catch(() => null)

  if (!response.ok) {
    // A body that is not the documented shape still has to become a usable error, so an
    // HTML error page from an intermediary degrades to the status instead of throwing on
    // a missing property.
    throw new ApiError({
      code: typeof data?.code === 'string' ? data.code : ServerErrorCode,
      message:
        typeof data?.message === 'string'
          ? data.message
          : i18n.t(`messages.api.${ServerErrorCode}`, { status: response.status }),
      status: response.status,
      errors: data?.errors && typeof data.errors === 'object' ? data.errors : null,
    })
  }

  return data ?? {}
}

/**
 * The text to show for a rejection: this build's translation of the code, the server's
 * English fallback when there is none, and a generic line when even that is missing.
 */
export function messageForError(error, fallbackKey = 'messages.sendFailed') {
  if (!(error instanceof ApiError)) {
    return error?.message || i18n.t(fallbackKey)
  }

  return (
    textForCode(error.code, { status: error.status }) ||
    error.message ||
    i18n.t(`messages.api.${ServerErrorCode}`, { status: error.status })
  )
}

/**
 * Field errors as antd's `setFields` wants them: an array of `{ name, errors }`, which is
 * what puts the reason under the input that caused it instead of in a toast.
 *
 * Unknown field names are dropped - a form that does not render a field has nowhere to
 * put its error - and any reason that is not an array is wrapped, because the server is
 * free to send either shape and antd only reads arrays.
 */
export function fieldErrors(errors) {
  if (!errors || typeof errors !== 'object') return []

  return Object.entries(errors)
    .filter(([, reasons]) => Array.isArray(reasons) && reasons.length > 0)
    .map(([name, reasons]) => ({ name, errors: reasons }))
}