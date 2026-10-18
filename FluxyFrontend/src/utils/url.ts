import config from '../config'

/**
 * The one place an API path becomes a URL.
 *
 * It lives in its own module because two callers need it and they must not depend on each
 * other: `http.ts` posts through it and `csrf.ts` fetches the antiforgery token through it,
 * while `http.ts` also imports `csrfHeader` from `csrf.ts`. Putting `apiUrl` in either of
 * them would make that pair circular, and the cycle resolves in an order that silently
 * yields a stale binding - the kind of failure that looks like a wrong URL.
 *
 * This module imports nothing but `config`, so both of them can import it freely.
 */
export const apiUrl = (path: string): string => `${config.API_BASE_URL}${path}`

/**
 * A path as this application may navigate to it, or null when the value is not one.
 *
 * The `redirect` a server sends is a string from outside this process, and `navigate` will
 * follow whatever it holds. Two properties make it safe to follow: it has to *be* a path -
 * no scheme, no host - and it has to be *this site's* path, so `//host` cannot read as
 * protocol-relative. The whole of that distinction is one leading slash and then a second
 * one: `/admin/dashboard` is a route this router has, `//evil.example` is a different
 * origin wearing the same first character.
 *
 * The rule lives here rather than at the five call sites because it must be written once.
 * Each site then decides only its own fallback - its own landing path, or the sign-in form
 * - which is a fact about that page and not about URL safety.
 *
 * This module still imports nothing but `config`, so `http.ts` and the pages can both use
 * this without closing the cycle the module exists to avoid.
 */
export const internalPath = (value: unknown): string | null => {
  if (typeof value !== 'string') return null
  if (!value.startsWith('/')) return null
  if (value.startsWith('//') || value.includes('\\')) return null
  // Control characters would be carried into the address bar and mean nothing to the
  // router; checked by code rather than with a regex so the reason is next to the test.
  for (let i = 0; i < value.length; i++) {
    if (value.charCodeAt(i) < 0x20) return null
  }
  return value
}
