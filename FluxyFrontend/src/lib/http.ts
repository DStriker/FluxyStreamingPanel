import i18n from '../i18n'
import { csrfHeader, getCsrfToken } from './csrf'
import { apiUrl, internalPath } from './url'
import type { TOptions } from 'i18next'
import type { FieldErrors, MessageResponse } from '../types'

/**
 * Codes for the two failures that never reached the API, so the branches below read the
 * same whether a code came back from the server or was invented here. The first is this
 * process talking to nobody, the second is a response that was not the documented body - a
 * proxy error page, a gateway 502, anything that is not the API.
 */
const NetworkErrorCode = 'network_error'
const ServerErrorCode = 'server_error'

/** The server says a session is gone and a refresh cannot rescue it. */
const SessionExpiredCode = 'session_expired'

/** The server says a session is needed and none was presented. */
const AuthRequiredCode = 'auth_required'

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
 * The options a request carries. `body` is `unknown` because it is JSON-serialized without
 * being looked at, and the token fields are `string | null` rather than `string` because
 * `getCsrfToken` and `getCaptchaToken` both answer `null` when they cannot produce one -
 * which is a state the caller passes through, not an error to fix at the call site.
 */
interface RequestOptions {
  body?: unknown
  csrfToken?: string | null
  captchaToken?: string | null
}

/** A request as `rawFetch` makes it: a `RequestOptions` plus the method. */
interface RawOptions extends RequestOptions {
  method?: 'GET' | 'POST'
}

/** Everything `ApiError` is constructed from. */
interface ApiErrorInit {
  code: string
  message: string
  status: number
  errors?: FieldErrors
  redirect?: string | null
}

/**
 * A failed call, carrying the machine readable `code` the server chose rather than only a
 * sentence.
 *
 * The code is what a caller should branch on, and it is what the localized text is looked
 * up by. `message` stays the server's English fallback for a code this build has no
 * translation for, which is why it is kept instead of being replaced at the throw site.
 *
 * The four fields are declared here rather than assigned only in the constructor: under
 * `strict` a property the constructor fills in has to be declared before it can be
 * written, and declaring them puts the shape of the error at the top of the class instead
 * of three-quarters of the way down it.
 */
export class ApiError extends Error {
  /** Machine readable outcome - what callers branch on and what locales translate. */
  readonly code: string
  /** HTTP status of the refusal, or `0` when no response arrived at all. */
  readonly status: number
  /** Rejected fields as `{ fieldName: [reason, ...] }`, or null when nothing was rejected. */
  readonly errors: FieldErrors
  /**
   * Path the server says this visitor belongs on, or null.
   *
   * The server names the destination rather than redirecting, and that is not a stylistic
   * choice: this API answers in JSON, and a `fetch` that follows a 302 ends up holding an
   * HTML page it cannot parse, so a real redirect would reach this application as a parse
   * failure with no status and no body. A path is also a smaller thing to trust - the
   * server does not know which host serves the page, so it cannot and does not send one.
   *
   * "A path" is enforced, not assumed: `internalPath` decides it, so a value that is not a
   * path this application can walk to - a scheme, a host, `//elsewhere` - arrives here as
   * `null` and the caller falls back to its own landing page. The same guard runs on the
   * success path, at the five `navigate(internalPath(...))` call sites.
   */
  readonly redirect: string | null

  constructor({ code, message, status, errors = null, redirect = null }: ApiErrorInit) {
    super(message)
    this.name = 'ApiError'
    this.code = code
    this.status = status
    this.errors = errors
    this.redirect = internalPath(redirect)
  }
}

/** Absolute URL of an API path, honouring the configured base. */
export { apiUrl }

/** Key in the locale files a server code is translated under, or null when there is none. */
const codeKey = (code: unknown): string | null =>
  typeof code === 'string' && code ? `messages.api.${code}` : null

/**
 * Localized text for a server `code`, or `null` when this build has no translation for it.
 * Callers decide what to show in that case rather than being handed a raw i18next key.
 *
 * The parameter is `unknown` rather than `string` because the thing being looked up is
 * read off a response body: a proxy page, a gateway 502 or an empty answer all reach this
 * function, and typing it `string` would only move the check into its callers. `codeKey`
 * is where the value is decided to be a code at all.
 */
export const textForCode = (code: unknown, options?: TOptions): string | null => {
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
let refreshInFlight: Promise<boolean> | null = null

/**
 * Exchanges the stored refresh token for a new pair, at most one attempt at a time.
 *
 * @returns whether a new pair was issued.
 */
async function refreshSession(): Promise<boolean> {
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
export async function apiFetch(
  path: string,
  { body, csrfToken, captchaToken }: RequestOptions = {},
): Promise<MessageResponse> {
  try {
    return await rawFetch<MessageResponse>(path, { body, csrfToken, captchaToken })
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
    return rawFetch<MessageResponse>(path, {
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
export async function apiGet<T>(path: string): Promise<T> {
  try {
    return await rawFetch<T>(path, { method: 'GET' })
  } catch (error) {
    if (!(error instanceof ApiError)) throw error

    const recoverable =
      error.status === 401 &&
      (error.code === SessionExpiredCode || error.code === AuthRequiredCode) &&
      !NO_RETRY_PATHS.has(path)

    if (!recoverable || !(await refreshSession())) {
      throw error
    }

    return rawFetch<T>(path, { method: 'GET' })
  }
}

/**
 * One request, and one answer. This is the part everything else is built on; `apiFetch` and
 * `apiGet` add the session handling around it.
 */
async function rawFetch<T>(
  path: string,
  { method, body, csrfToken, captchaToken }: RawOptions = {},
): Promise<T> {
  const isWrite = method !== 'GET'

  let response: Response

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

  const data: unknown = await response.json().catch(() => null)

  // The body, as far as this file can rely on it. Read once into the documented shape so
  // that the checks below are property reads on a known type rather than on `unknown` -
  // and still guarded, because every one of them exists for a body that is *not* this
  // shape: a proxy error page, a gateway 502, an empty answer. Named `payload` rather than
  // `body`, which the request's own payload has already claimed above.
  const payload = data as Partial<MessageResponse> | null

  // A 2xx whose body is not JSON. In development the dev server answers an unknown path
  // with the SPA shell and a 200, so this is the shape a typo'd API prefix takes: `ok` is
  // true, the parse fails, and without this the call would return `{}` - a successful,
  // empty answer to a question that was never asked. Nothing else reports it, and every
  // API in this backend answers 2xx with a body, so the case is unambiguous.
  if (response.ok && data === null) {
    throw new ApiError({
      code: ServerErrorCode,
      message: i18n.t(`messages.api.${ServerErrorCode}`, { status: response.status }),
      status: response.status,
    })
  }

  if (!response.ok) {
    // A body that is not the documented shape still has to become a usable error, so an
    // HTML error page from an intermediary degrades to the status instead of throwing on
    // a missing property.
    throw new ApiError({
      code: typeof payload?.code === 'string' ? payload.code : ServerErrorCode,
      message:
        typeof payload?.message === 'string'
          ? payload.message
          : i18n.t(`messages.api.${ServerErrorCode}`, { status: response.status }),
      status: response.status,
      errors: payload?.errors && typeof payload.errors === 'object' ? payload.errors : null,
      redirect: typeof payload?.redirect === 'string' ? payload.redirect : null,
    })
  }

  return (data ?? {}) as T
}

/**
 * The `message` of whatever was thrown, when it has one that is a string.
 *
 * Not `error instanceof Error`: a `catch` in TypeScript has the type `unknown` for a
 * reason - anything at all can be thrown, including a plain object with a `message` on it,
 * which is what a library that predates `Error` hands out. Reading it through this shape
 * check keeps the original `error?.message || fallback` for those without narrowing a type
 * it never had.
 */
const messageOf = (error: unknown): string => {
  if (error === null || typeof error !== 'object') return ''
  const { message } = error as { message?: unknown }
  return typeof message === 'string' ? message : ''
}

/**
 * The text to show for a rejection: this build's translation of the code, the server's
 * English fallback when there is none, and a generic line when even that is missing.
 */
export function messageForError(error: unknown, fallbackKey = 'messages.sendFailed'): string {
  if (!(error instanceof ApiError)) {
    return messageOf(error) || i18n.t(fallbackKey)
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
 * A name survives only with something to say: an empty list, or a value that is not a list
 * at all, contributes nothing - an error with no reasons would put an empty red outline
 * under an input, which reads as a rejection the server never made. The key of each entry
 * is the camelCase JSON property name, deliberately the same name the `Form.Item`s use,
 * so `setFields` needs no translation step between the two.
 *
 * `unknown` because the value comes off a response body, and the return type is written
 * out rather than left to inference because it is a *contract with antd*: `setFields`
 * takes `FieldData[]`, and a `{ name, errors: any[] }` that happens to fit is not the same
 * claim as one that is declared to.
 */
export function fieldErrors(errors: unknown): { name: string; errors: string[] }[] {
  if (!errors || typeof errors !== 'object') return []

  return Object.entries(errors).flatMap(([name, reasons]) =>
    Array.isArray(reasons) && reasons.length > 0 ? [{ name, errors: reasons as string[] }] : [],
  )
}