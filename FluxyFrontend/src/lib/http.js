import i18n from '../i18n'
import { csrfHeader, getCsrfToken } from './csrf'
import { apiUrl } from './url'

/**
 * Codes for the two failures that never reached the API, so a caller can branch on them
 * the same way it branches on a server code. The first is this process talking to nobody,
 * the second is a response that was not the documented body - a proxy error page, a
 * gateway 502, anything that is not the API.
 */
export const NetworkErrorCode = 'network_error'
export const ServerErrorCode = 'server_error'

/** The server says a session is gone and a refresh cannot rescue it. */
export const SessionExpiredCode = 'session_expired'

/** The server says a session is needed and none was presented. */
export const AuthRequiredCode = 'auth_required'

/**
 * Endpoints that must never be retried after a refresh, and endpoints that must never
 * trigger one.
 *
 * The first group is `refresh` itself: retrying it would ask for a new token with the token
 * that was just spent, which the server reads as a replay and answers by destroying the
 * whole chain. The second group is `refresh` and the sign-in forms, where a 401 is the
 * answer rather than a symptom - "your password is wrong" must not turn into "let me try
 * to refresh a session that does not exist".
 *
 * `logout` is deliberately in neither group, now that the endpoint requires a session. Its
 * 401 means the access token is gone, and refreshing first is what keeps a sign-out from
 * stranding the visitor: the refresh succeeds and the retry carries a usable token (and a
 * token minted for the identity that rotation just produced); when there is nothing to
 * refresh, the refresh fails, the original refusal reaches the caller unchanged, and the
 * server has cleared whatever dead cookies the attempt presented.
 */
const NO_REFRESH_PATHS = new Set(['/auth/refresh'])
const NO_RETRY_PATHS = new Set([
  '/auth/refresh',
  '/auth/client-login',
  '/auth/reseller-login',
  '/auth/admin-login',
])

/**
 * A failed call, carrying the machine readable `code` the server chose rather than only a
 * sentence.
 *
 * The code is what a caller should branch on, and it is what the localized text is looked
 * up by. `message` stays the server's English fallback for a code this build has no
 * translation for, which is why it is kept instead of being replaced at the throw site.
 */
export class ApiError extends Error {
  constructor({ code, message, status, errors = null, redirect = null }) {
    super(message)
    this.name = 'ApiError'
    this.code = code
    this.status = status
    /** Rejected fields as `{ fieldName: [reason, ...] }`, or null when nothing was rejected. */
    this.errors = errors
    /**
     * Path the server says this visitor belongs on, or null.
     *
     * The server names the destination rather than redirecting, and that is not a stylistic
     * choice: this API answers in JSON, and a `fetch` that follows a 302 ends up holding an
     * HTML page it cannot parse, so a real redirect would reach this application as a parse
     * failure with no status and no body. A path is also a smaller thing to trust - the
     * server does not know which host serves the page, so it cannot and does not send one.
     */
    this.redirect = typeof redirect === 'string' && redirect ? redirect : null
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
 * The one refresh attempt that several failing calls share.
 *
 * A page that loads six things at once will get six 401s from six access tokens that all
 * expired in the same five minutes, and each of them will try to refresh. Because a refresh
 * token is single use, five of those six would present a token the first one had already
 * spent - and the server reads a second presentation as a stolen token and destroys the
 * whole session. So the promise is kept here: the first caller starts the attempt, the rest
 * await the same one, and the result is the same for all of them.
 */
let refreshInFlight = null

/**
 * Exchanges the stored refresh token for a new pair, at most one attempt at a time.
 *
 * @returns {Promise<boolean>} whether a new pair was issued.
 */
async function refreshSession() {
  if (!refreshInFlight) {
    refreshInFlight = (async () => {
      try {
        await rawFetch('/auth/refresh', {
          method: 'POST',
          csrfToken: await getCsrfToken(),
        })

        return true
      } catch {
        // Deliberately not rethrown. The caller only needs to know whether the retry is
        // worth making, and the failure that mattered - the original 401 - is already on
        // its way to the page that made the call.
        return false
      } finally {
        refreshInFlight = null
      }
    })()
  }

  return refreshInFlight
}

/**
 * Posts to the API and turns any non-2xx into an {@link ApiError}.
 *
 * Every call goes through here so that a rejection always has the same shape: a code to
 * branch on, text to show, and - when the server rejected specific fields - the map of
 * them keyed by the camelCase name of the JSON property, which is also the name the form
 * fields use.
 *
 * A 401 caused by an expired access token is retried once, transparently, after a
 * successful refresh. That is the whole reason the access token lives five minutes and the
 * refresh token a week: the short one is expected to expire in the middle of a session and
 * the page should not know about it. The retry happens at most once per call, and only for
 * paths where a retry is meaningful.
 */
export async function apiFetch(path, { body, csrfToken, captchaToken } = {}) {
  try {
    return await rawFetch(path, { body, csrfToken, captchaToken })
  } catch (error) {
    if (!(error instanceof ApiError)) throw error

    const recoverable =
      error.status === 401 &&
      (error.code === SessionExpiredCode || error.code === AuthRequiredCode) &&
      !NO_REFRESH_PATHS.has(path) &&
      !NO_RETRY_PATHS.has(path)

    if (!recoverable || !(await refreshSession())) {
      throw error
    }

    // The CSRF pair does not change when the session is rotated - it is a separate scheme
    // with its own cookie - but a token is minted again anyway, because the one the failed
    // call carried was minted under whatever identity was current then, and that is exactly
    // the kind of detail a retry must not inherit.
    return rawFetch(path, {
      body,
      csrfToken: await getCsrfToken(),
      captchaToken,
    })
  }
}

/**
 * Gets from the API and turns any non-2xx into an {@link ApiError}, with the same
 * refresh-and-retry behaviour as {@link apiFetch}.
 *
 * Separate because the method is not a parameter worth exposing at every call site: there
 * are two GETs in this application and both of them want the retry, and a boolean argument
 * at each of them would be an easy thing to forget to pass.
 */
export async function apiGet(path) {
  try {
    return await rawFetch(path, { method: 'GET' })
  } catch (error) {
    if (!(error instanceof ApiError)) throw error

    const recoverable =
      error.status === 401 &&
      (error.code === SessionExpiredCode || error.code === AuthRequiredCode) &&
      !NO_RETRY_PATHS.has(path)

    if (!recoverable || !(await refreshSession())) {
      throw error
    }

    return rawFetch(path, { method: 'GET' })
  }
}

/**
 * One request, and one answer. This is the part everything else is built on; `apiFetch` and
 * `apiGet` add the session handling around it.
 */
async function rawFetch(path, { method, body, csrfToken, captchaToken } = {}) {
  const isWrite = method !== 'GET'

  let response

  try {
    response = await fetch(apiUrl(path), {
      method: method ?? 'POST',
      // The cookies are the reason this is 'include' and not the default 'same-origin'.
      // They are `SameSite=Lax` and `HttpOnly`, which is what keeps a session out of reach
      // of a script on the page; same-origin in development means the browser sends them
      // without being asked, and a split deployment would need exactly this.
      credentials: 'include',
      headers: {
        ...(isWrite ? { 'Content-Type': 'application/json' } : {}),
        // Sent only when there is one. In dev the captcha is skipped and produces no
        // token, and an empty header would only invite the question of whether it means
        // "no captcha" or "a broken captcha".
        ...(captchaToken ? { 'X-Recaptcha-Token': captchaToken } : {}),
        ...(isWrite ? csrfHeader(csrfToken) : {}),
      },
      ...(isWrite ? { body: JSON.stringify(body ?? {}) } : {}),
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
      redirect: typeof data?.redirect === 'string' ? data.redirect : null,
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