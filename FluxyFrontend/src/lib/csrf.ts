import { apiUrl } from './url'

const COOKIE_NAME = 'XSRF-TOKEN'
const TOKEN_URL = '/auth/csrf'

const readCookie = (name: string): string | null => {
  const match = document.cookie.match(
    new RegExp(`(?:^|; )${name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}=([^;]*)`),
  )
  // `match[1]` is typed as possibly missing under `noUncheckedIndexedAccess`; a pattern
  // that matched has captured it, and `decodeURIComponent(undefined)` would be the wrong
  // way to find out either way, so the guard states what the match already proved.
  return match?.[1] === undefined ? null : decodeURIComponent(match[1])
}

/**
 * Mints an antiforgery token for the identity the request will arrive with.
 *
 * Always a round trip to `GET /auth/csrf`, never a cached value and never the cookie read
 * back. That is a correctness rule rather than a preference: the server binds a token to
 * the claims-based user that was current when it was minted, so a token obtained on the
 * sign-in page is refused by any endpoint called *after* sign-in - the answer is
 * `csrf_invalid`, and the server log names the reason as "meant for a different claims-based
 * user than the current user". The identity changes at sign-in, sign-out and confirmation,
 * and can lapse on its own when the access token expires, so the only token guaranteed to
 * match the one being sent is a token minted immediately before sending it.
 *
 * The server drops the readable `XSRF-TOKEN` cookie whenever the session changes (see
 * `AuthCookies` on the backend), which keeps the cookie from outliving its identity even
 * for a client that reads it - but this function no longer reads it first, because a copy
 * that survives an access token expiring is just as stale and nothing marks it as such.
 *
 * The cookie is still the fallback when the server cannot be reached at all, so a caller
 * that has *some* token keeps it best-effort rather than failing outright.
 */
export async function getCsrfToken(): Promise<string | null> {
  try {
    // Through `apiUrl`, not the bare path. A relative '/auth/csrf' goes to whatever is
    // serving the page - the Vite dev server in development - which answers the SPA shell
    // with a 200, so the token lookup silently yields nothing and every later POST comes
    // back `csrf_invalid`. Same `credentials: 'include'` as `apiFetch`, because the
    // antiforgery cookies live on the API's origin, not the page's.
    const res = await fetch(apiUrl(TOKEN_URL), {
      credentials: 'include',
      headers: { Accept: 'application/json' },
    })
    if (res.ok) {
      // `token` is what the endpoint answers with (`CsrfTokenResponse`); `csrfToken` is
      // accepted as well so the lookup does not depend on one spelling of the field.
      // `unknown` rather than `any` so the value is narrowed below instead of being
      // handed to a caller as a `string` nobody checked.
      const data: { token?: unknown; csrfToken?: unknown } | null = await res.json().catch(
        () => null,
      )
      const value = data?.token ?? data?.csrfToken ?? readCookie(COOKIE_NAME) ?? null
      return typeof value === 'string' ? value : null
    }
  } catch {
    // backend не подключён — работаем без токена
  }

  return readCookie(COOKIE_NAME)
}

export function csrfHeader(token?: string | null): Record<string, string> {
  return token ? { 'X-CSRF-Token': token } : {}
}
