const COOKIE_NAME = 'XSRF-TOKEN'
const TOKEN_URL = '/api/auth/csrf'

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
    const res = await fetch(TOKEN_URL, {
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
