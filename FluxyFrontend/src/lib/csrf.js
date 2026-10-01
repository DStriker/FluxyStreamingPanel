import { apiUrl } from './url'

const COOKIE_NAME = 'XSRF-TOKEN'
const TOKEN_URL = '/auth/csrf'

let cachedToken = null

const readCookie = (name) => {
  const match = document.cookie.match(
    new RegExp(`(?:^|; )${name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}=([^;]*)`),
  )
  return match ? decodeURIComponent(match[1]) : null
}

export async function getCsrfToken({ refresh = false } = {}) {
  if (!refresh && cachedToken) return cachedToken

  const fromCookie = readCookie(COOKIE_NAME)
  if (fromCookie) {
    cachedToken = fromCookie
    return cachedToken
  }

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
      const data = await res.json().catch(() => null)
      cachedToken = data?.token ?? data?.csrfToken ?? readCookie(COOKIE_NAME) ?? null
      return cachedToken
    }
  } catch {
    // backend не подключён — работаем без токена
  }

  cachedToken = readCookie(COOKIE_NAME)
  return cachedToken
}

export function csrfHeader(token) {
  return token ? { 'X-CSRF-Token': token } : {}
}
